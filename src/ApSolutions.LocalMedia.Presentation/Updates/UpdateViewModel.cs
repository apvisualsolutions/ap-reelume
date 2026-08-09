using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Presentation.Updates;

/// <summary>
/// The screen an update is read on and confirmed from.
/// </summary>
/// <remarks>
/// Three separate acts, in this order and no other: ask, fetch, confirm. They are three buttons
/// rather than one because they mean different things — asking is curiosity, fetching costs
/// bandwidth, and confirming replaces the application somebody is using.
/// <para>
/// This is the only place a <see cref="UpdateConsent"/> is constructed, and it is constructed from
/// the version currently on screen at the moment the button is pressed. That is what makes the
/// confirmation a confirmation of what was read rather than of whatever happens to be staged.
/// </para>
/// <para>
/// The package is named and its folder never is. A path on screen is how a screenshot of an update
/// dialogue stops being safe to send to somebody.
/// </para>
/// </remarks>
public sealed class UpdateViewModel : INotifyPropertyChanged
{
    private readonly Func<UpdateCheckTrigger, CancellationToken, Task<UpdateCheckResult>> _check;
    private readonly Func<UpdateRelease, IProgress<UpdateDownloadProgress>, CancellationToken, Task<StagedUpdate>> _stage;
    private readonly Func<StagedUpdate, UpdateConsent?, CancellationToken, Task<UpdateInstallation>> _install;
    private readonly Func<DateTimeOffset> _now;
    private readonly IUpdateSettings _settings;
    private readonly IProgress<UpdateDownloadProgress> _progress;
    private readonly UpdateCommand _checkCommand;
    private readonly UpdateCommand _downloadCommand;
    private readonly UpdateCommand _installCommand;
    private readonly UpdateCommand _cancelCommand;
    private CancellationTokenSource? _cancellation;
    private UpdateRelease? _offer;
    private StagedUpdate? _staged;
    private bool _isBusy;
    private string _statusKey = "UpdateStatusIdle";
    private string? _detailKey;
    private long _completed;
    private long _total;

    public UpdateViewModel(
        Func<UpdateCheckTrigger, CancellationToken, Task<UpdateCheckResult>> check,
        Func<UpdateRelease, IProgress<UpdateDownloadProgress>, CancellationToken, Task<StagedUpdate>> stage,
        Func<StagedUpdate, UpdateConsent?, CancellationToken, Task<UpdateInstallation>> install,
        Func<DateTimeOffset> now,
        IUpdateSettings settings)
    {
        _check = check ?? throw new ArgumentNullException(nameof(check));
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));
        _install = install ?? throw new ArgumentNullException(nameof(install));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _progress = new ImmediateProgress(Apply);
        _checkCommand = new UpdateCommand(() => _ = CheckAsync(CancellationToken.None), () => !IsBusy);
        _downloadCommand = new UpdateCommand(
            () => _ = DownloadAsync(CancellationToken.None),
            () => !IsBusy && HasOffer);
        _installCommand = new UpdateCommand(
            () => _ = InstallAsync(CancellationToken.None),
            () => !IsBusy && IsReadyToInstall);
        _cancelCommand = new UpdateCommand(Cancel, () => IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand CheckCommand => _checkCommand;

    public ICommand DownloadCommand => _downloadCommand;

    public ICommand InstallCommand => _installCommand;

    public ICommand CancelCommand => _cancelCommand;

    /// <summary>A resource key rather than a sentence, so the screen speaks the chosen language.</summary>
    public string StatusKey
    {
        get => _statusKey;
        private set => SetField(ref _statusKey, value);
    }

    /// <summary>
    /// Which rule refused a release, when one did. It is separate from the status because "this
    /// release cannot be used" and "because its notes carry no hash" are two different sentences, and
    /// only the second one tells whoever published it what to fix.
    /// </summary>
    public string? DetailKey
    {
        get => _detailKey;
        private set => SetField(ref _detailKey, value);
    }

    /// <summary>
    /// Whether one of the three steps is running. The setter announces unconditionally because it is
    /// private and only ever moved between the two states: a guard here would be a branch nothing
    /// could ever take.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsIdle));

            // A button bound to a command asks once and then waits to be told. Without this the
            // cancel button would never become usable, however long the download ran.
            RaiseCanExecuteChanged();
        }
    }

    public bool IsIdle => !IsBusy;

    /// <summary>
    /// Whether the application may look on its own. It is off until somebody turns it on, because a
    /// check is a connection and this application makes none it was not asked to make.
    /// </summary>
    public bool AutomaticCheckEnabled
    {
        get => _settings.AutomaticCheckEnabled;
        set
        {
            if (value == _settings.AutomaticCheckEnabled)
            {
                return;
            }

            _settings.SetAutomaticCheckEnabled(value);
            OnPropertyChanged();
        }
    }

    public bool HasOffer => _offer is not null;

    public bool IsReadyToInstall => _staged is not null;

    public string? OfferedVersion => _offer?.Version;

    public string? SummarySpanish => _offer?.SummaryEs;

    public string? SummaryEnglish => _offer?.SummaryEn;

    /// <summary>
    /// What changed, in the language the application is running in. Spanish only when the interface
    /// is in Spanish: English is what everybody else gets, because a summary somebody cannot read
    /// turns the confirmation into a formality.
    /// </summary>
    public string? Summary =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase)
            ? SummarySpanish
            : SummaryEnglish;

    /// <summary>The name of the package, never the folder it is in.</summary>
    public string? PackageName => _staged is null ? null : Path.GetFileName(_staged.Path);

    public long Completed
    {
        get => _completed;
        private set => SetField(ref _completed, value);
    }

    public long Total
    {
        get => _total;
        private set => SetField(ref _total, value);
    }

    /// <summary>
    /// Asks the source what it has. Everything staged for a previous answer is dropped first: a
    /// button that still installs yesterday's package while the screen describes today's is the
    /// worst possible version of this surface.
    /// </summary>
    public Task CheckAsync(CancellationToken cancellationToken = default) =>
        CheckAsync(UpdateCheckTrigger.Requested, cancellationToken);

    /// <summary>
    /// The check the application runs on its own, if it has been allowed to. It exists because a
    /// preference nothing acts on is not a preference: the switch was on the screen and on disk, and
    /// no code path in the application ever asked for an automatic check.
    /// </summary>
    public Task CheckAutomaticallyAsync(CancellationToken cancellationToken = default) =>
        CheckAsync(UpdateCheckTrigger.Automatic, cancellationToken);

    private async Task CheckAsync(UpdateCheckTrigger trigger, CancellationToken cancellationToken)
    {
        if (!TryBegin(cancellationToken, out var cancellation))
        {
            return;
        }

        Forget();
        StatusKey = "UpdateStatusChecking";
        try
        {
            Apply(await _check(trigger, cancellation.Token).ConfigureAwait(true));
        }
        catch (OperationCanceledException)
        {
            StatusKey = "UpdateStatusCancelled";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The automatic check runs on the interface thread at startup: whatever the source
            // answered, the surface lands on a state instead of taking the application down or
            // reading "checking…" forever.
            StatusKey = "UpdateStatusUnreachable";
        }
        finally
        {
            Complete(cancellation);
        }
    }

    /// <summary>
    /// Fetches the offered package. It installs nothing: what this produces is a file and a proof
    /// about it.
    /// </summary>
    public async Task DownloadAsync(CancellationToken cancellationToken = default)
    {
        if (_offer is not { } release || !TryBegin(cancellationToken, out var cancellation))
        {
            return;
        }

        Completed = 0;
        Total = release.SizeInBytes;
        StatusKey = "UpdateStatusDownloading";
        try
        {
            _staged = await _stage(release, _progress, cancellation.Token).ConfigureAwait(true);
            StatusKey = "UpdateStatusReady";
            OnPropertyChanged(nameof(IsReadyToInstall));
            OnPropertyChanged(nameof(PackageName));
        }
        catch (OperationCanceledException)
        {
            StatusKey = "UpdateStatusCancelled";
        }
        catch (UpdateInterruptedException)
        {
            // The offer stays on screen: what arrived is still on disk, so trying again resumes
            // rather than starting over, and the button to do it has to still be there.
            StatusKey = "UpdateStatusInterrupted";
        }
        catch (UpdateVerificationException)
        {
            StatusKey = "UpdateStatusVerificationFailed";
        }
        catch (UpdateRefusedException refusal)
        {
            // A refusal at download time — a hop outside the declared hosts — is a decision with a
            // name, not a network that failed; the screen says which rule refused it.
            StatusKey = "UpdateStatusUnusableRelease";
            DetailKey = $"UpdateRefused{refusal.Rejection}";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusKey = "UpdateStatusUnreachable";
        }
        finally
        {
            Complete(cancellation);
        }
    }

    /// <summary>
    /// The confirmation. It is the only thing in this application that constructs a consent, and it
    /// constructs one for the version currently on screen.
    /// </summary>
    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (_staged is not { } staged || !TryBegin(cancellationToken, out var cancellation))
        {
            return;
        }

        try
        {
            var installation = await _install(
                staged,
                new UpdateConsent(staged.Release.Version ?? string.Empty, _now()),
                cancellation.Token).ConfigureAwait(true);
            StatusKey = Describe(installation.Outcome);
            if (installation.Outcome is UpdateOutcome.Tampered)
            {
                Forget();
            }
        }
        catch (OperationCanceledException)
        {
            StatusKey = "UpdateStatusCancelled";
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            StatusKey = "UpdateStatusLaunchRefused";
        }
        finally
        {
            Complete(cancellation);
        }
    }

    private static string Describe(UpdateOutcome outcome) => outcome switch
    {
        UpdateOutcome.HandedToWindows => "UpdateStatusHandedToWindows",
        UpdateOutcome.LaunchRefused => "UpdateStatusLaunchRefused",
        UpdateOutcome.Tampered => "UpdateStatusTampered",
        _ => "UpdateStatusNotConfirmed",
    };

    /// <summary>
    /// Turns one answer into what the screen says. Having nothing newer and having asked a source
    /// that published nothing are the same news; the other refusals are problems with the release
    /// itself, and each one is named.
    /// </summary>
    private void Apply(UpdateCheckResult result)
    {
        if (result.HasOffer)
        {
            _offer = result.Release;
            StatusKey = "UpdateStatusOffered";
            OnPropertyChanged(nameof(HasOffer));
            OnPropertyChanged(nameof(OfferedVersion));
            OnPropertyChanged(nameof(SummarySpanish));
            OnPropertyChanged(nameof(SummaryEnglish));
            OnPropertyChanged(nameof(Summary));
            return;
        }

        StatusKey = result.Status switch
        {
            UpdateCheckStatus.NotAsked => "UpdateStatusIdle",
            UpdateCheckStatus.Unreachable => "UpdateStatusUnreachable",
            _ => result.Decision?.Rejection switch
            {
                UpdateRejection.InsecureDownload
                    or UpdateRejection.UnusableHash
                    or UpdateRejection.UnsignedChecksums
                    or UpdateRejection.WrongRuntime
                    or UpdateRejection.UndeclaredSize
                    or UpdateRejection.IncompleteSummary => "UpdateStatusUnusableRelease",
                _ => "UpdateStatusUpToDate",
            },
        };
        DetailKey = StatusKey == "UpdateStatusUnusableRelease"
            ? $"UpdateRefused{result.Decision!.Rejection}"
            : null;
    }

    private void Apply(UpdateDownloadProgress progress)
    {
        Completed = progress.BytesReceived;
        Total = progress.TotalBytes;
    }

    /// <summary>Drops the offer and anything staged for it, and says so to everything bound to them.</summary>
    private void Forget()
    {
        _offer = null;
        _staged = null;
        DetailKey = null;
        Completed = 0;
        Total = 0;
        OnPropertyChanged(nameof(HasOffer));
        OnPropertyChanged(nameof(IsReadyToInstall));
        OnPropertyChanged(nameof(OfferedVersion));
        OnPropertyChanged(nameof(SummarySpanish));
        OnPropertyChanged(nameof(SummaryEnglish));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(PackageName));
    }

    private bool TryBegin(CancellationToken cancellationToken, out CancellationTokenSource cancellation)
    {
        cancellation = null!;
        if (IsBusy)
        {
            return false;
        }

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation = _cancellation;
        IsBusy = true;
        return true;
    }

    /// <summary>
    /// Stops whatever is running. Pressing this with nothing running is not an error: the button is
    /// disabled then, but a surface whose only defence is a disabled button is one edit away from
    /// not having one.
    /// </summary>
    public void Cancel() => _cancellation?.Cancel();

    private void Complete(CancellationTokenSource cancellation)
    {
        IsBusy = false;
        cancellation.Dispose();
        _cancellation = null;
        RaiseCanExecuteChanged();
    }

    private void RaiseCanExecuteChanged()
    {
        _checkCommand.RaiseCanExecuteChanged();
        _downloadCommand.RaiseCanExecuteChanged();
        _installCommand.RaiseCanExecuteChanged();
        _cancelCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// Reports on the thread that produced the step, as the backup surface does. A progress bar that
    /// arrives after the download finished is not progress.
    /// </summary>
    private sealed class ImmediateProgress(Action<UpdateDownloadProgress> report)
        : IProgress<UpdateDownloadProgress>
    {
        public void Report(UpdateDownloadProgress value) => report(value);
    }

    private sealed class UpdateCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter)
        {
            if (canExecute())
            {
                execute();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
