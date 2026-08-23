// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Privacy;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// The privacy screen: what is sent, when, and the switch that decides it.
/// <para>
/// Diagnostics are off until somebody turns them on here, and turning them off again clears both the
/// consent and whatever preview was on screen — taking a permission back must never be harder, or
/// slower, than giving it.
/// </para>
/// </summary>
public sealed class PrivacySettingsViewModel : INotifyPropertyChanged
{
    private readonly IPrivacySettings _settings;
    private readonly IDiagnosticsBuilder _builder;
    private readonly Func<DiagnosticsInputs> _inputs;
    private readonly Func<DiagnosticsConsent, DiagnosticsInputs, CancellationToken, Task<string?>> _export;
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<CancellationToken, Task>? _discard;
    private readonly IAutoRefreshSettings? _autoRefresh;
    private readonly Func<bool> _hasConsentedConnection;
    private readonly PrivacyCommand _preview;
    private readonly PrivacyCommand _exportCommand;
    private string? _previewJson;
    private string _statusKey = "PrivacyStatusOff";
    private string? _exportedFileName;

    public PrivacySettingsViewModel(
        IPrivacySettings settings,
        IDiagnosticsBuilder builder,
        Func<DiagnosticsInputs> inputs,
        Func<DiagnosticsConsent, DiagnosticsInputs, CancellationToken, Task<string?>> export,
        Func<DateTimeOffset> now,
        IReadOnlyList<NetworkPurpose> purposes,
        Func<CancellationToken, Task>? discard = null,
        IAutoRefreshSettings? autoRefresh = null,
        Func<bool>? hasConsentedConnection = null)
    {
        _autoRefresh = autoRefresh;
        _hasConsentedConnection = hasConsentedConnection ?? (() => false);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _discard = discard;
        NetworkPurposes = purposes ?? throw new ArgumentNullException(nameof(purposes));
        StatusKey = settings.Current.IsGranted ? "PrivacyStatusOn" : "PrivacyStatusOff";
        _preview = new PrivacyCommand(ShowPreview, () => true);
        _exportCommand = new PrivacyCommand(
            () => _ = ExportAsync(CancellationToken.None),
            () => DiagnosticsEnabled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PreviewCommand => _preview;

    public ICommand ExportCommand => _exportCommand;

    /// <summary>Every connection the application can make, so the screen can list them rather than promise.</summary>
    public IReadOnlyList<NetworkPurpose> NetworkPurposes { get; }

    /// <summary>
    /// True when nothing is declared at all - the best answer this page can give, painted in
    /// positive terms. The registry is fixed at composition, so this never moves at runtime.
    /// </summary>
    public bool HasNoDeclaredHosts => NetworkPurposes.Count == 0;

    /// <summary>
    /// Whether the automatic refresh can be offered at all. It is subordinate to the consented
    /// connection: without one, the provider serves only what it already cached, so a switch would
    /// promise something that cannot happen — and a preference nobody can act on is worse than an
    /// absent one.
    /// </summary>
    public bool CanRefreshAutomatically => _autoRefresh is not null && _hasConsentedConnection();

    /// <summary>
    /// Off until somebody turns it on, and off is also what it reads when there is nothing to read
    /// it from.
    /// </summary>
    public bool AutomaticRefreshEnabled
    {
        get => _autoRefresh?.AutomaticRefreshEnabled ?? false;
        set
        {
            if (_autoRefresh is not { } refresh || refresh.AutomaticRefreshEnabled == value)
            {
                return;
            }

            refresh.SetAutomaticRefreshEnabled(value);
            OnPropertyChanged();
        }
    }

    public bool DiagnosticsEnabled
    {
        get => _settings.Current.IsGranted;
        set
        {
            if (_settings.Current.IsGranted == value)
            {
                return;
            }

            _settings.Save(new DiagnosticsConsent(value, value ? _now() : null));
            if (!value)
            {
                PreviewJson = null;
                ExportedFileName = null;

                // Withdrawing has to reach the file, not only the screen. Clearing the name while the
                // report stays on disk is the appearance of taking a permission back, not the fact.
                _ = DiscardAsync(CancellationToken.None);
            }

            StatusKey = value ? "PrivacyStatusOn" : "PrivacyStatusOff";
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasPreview));
            _exportCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>The exact text an export would write, or nothing at all.</summary>
    public string? PreviewJson
    {
        get => _previewJson;
        private set
        {
            if (SetField(ref _previewJson, value))
            {
                OnPropertyChanged(nameof(HasPreview));
            }
        }
    }

    public bool HasPreview => PreviewJson is not null;

    public string StatusKey
    {
        get => _statusKey;
        private set => SetField(ref _statusKey, value);
    }

    /// <summary>The name of the file that was written, never the folder it went into.</summary>
    public string? ExportedFileName
    {
        get => _exportedFileName;
        private set => SetField(ref _exportedFileName, value);
    }

    public async Task ExportAsync(CancellationToken cancellationToken = default)
    {
        if (!DiagnosticsEnabled)
        {
            return;
        }

        string? written;
        try
        {
            written = await _export(_settings.Current, _inputs(), cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Saying nothing would be indistinguishable from success, and this is the one screen where
            // a person has to be able to tell those apart.
            ExportedFileName = null;
            StatusKey = "PrivacyStatusFailed";
            return;
        }

        if (written is null)
        {
            return;
        }

        ExportedFileName = Path.GetFileName(written);
        StatusKey = "PrivacyStatusExported";
    }

    /// <summary>
    /// Removes whatever a previous export left on disk. A folder that is already gone is the intended
    /// end state, so nothing is reported when there was nothing to remove.
    /// </summary>
    private async Task DiscardAsync(CancellationToken cancellationToken)
    {
        if (_discard is not { } discard)
        {
            return;
        }

        try
        {
            await discard(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusKey = "PrivacyStatusFailed";
        }
    }

    /// <summary>
    /// The preview is produced by the same builder and the same serializer the export uses. Building the
    /// screen's own version of the payload would be exactly how the two drift apart.
    /// </summary>
    private void ShowPreview()
    {
        try
        {
            var report = DiagnosticsEnabled ? _builder.Build(_settings.Current, _inputs()) : null;
            PreviewJson = report is null ? null : DiagnosticsSerialization.Serialize(report);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // A screen that does nothing when a button is pressed is indistinguishable from a screen
            // that is broken, and this is the screen where that ambiguity is least acceptable.
            PreviewJson = null;
            StatusKey = "PrivacyStatusFailed";
        }
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
    /// A command that tells the surface when its answer changes.
    /// <para>
    /// A button bound to a command asks once and then waits to be told. Without this event the export
    /// button stays disabled forever, however many times the switch is turned on — which is exactly
    /// what it did until the real application was driven by hand.
    /// </para>
    /// </summary>
    private sealed class PrivacyCommand(Action execute, Func<bool> canExecute) : ICommand
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
