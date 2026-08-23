// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Backup;

namespace ApSolutions.LocalMedia.Presentation.Backup;

/// <summary>
/// The copy and export screen. It reports which step is running, can be stopped at any point, and says
/// what happened afterwards.
/// <para>
/// Both results are named by their file name and never by their folder. A backup screen has no reason
/// to display where somebody keeps their things, and printing a path is how a screenshot stops being
/// safe to share.
/// </para>
/// </summary>
public sealed class BackupViewModel : INotifyPropertyChanged
{
    private readonly Func<IProgress<BackupProgress>, CancellationToken, Task<BackupResult>> _createCopy;
    private readonly Func<string, IProgress<BackupProgress>, CancellationToken, Task<ExportResult>> _export;
    private readonly Func<CancellationToken, Task<string?>> _chooseDestination;
    private readonly IProgress<BackupProgress> _progress;
    private readonly BackupCommand _createCopyCommand;
    private readonly BackupCommand _exportCommand;
    private readonly BackupCommand _cancelCommand;
    private CancellationTokenSource? _cancellation;
    private bool _isRunning;
    private string _statusKey = "BackupStatusIdle";
    private string? _stageKey;
    private int _completed;
    private int _total;
    private string? _lastCopyName;
    private string? _lastArchiveName;

    public BackupViewModel(
        Func<IProgress<BackupProgress>, CancellationToken, Task<BackupResult>> createCopy,
        Func<string, IProgress<BackupProgress>, CancellationToken, Task<ExportResult>> export,
        Func<CancellationToken, Task<string?>> chooseDestination,
        string? databasePath = null)
    {
        DatabasePath = databasePath;
        _createCopy = createCopy ?? throw new ArgumentNullException(nameof(createCopy));
        _export = export ?? throw new ArgumentNullException(nameof(export));
        _chooseDestination = chooseDestination ?? throw new ArgumentNullException(nameof(chooseDestination));
        _progress = new ImmediateProgress(Apply);
        _createCopyCommand = new BackupCommand(
            () => _ = CreateCopyAsync(CancellationToken.None),
            () => !IsRunning);
        _exportCommand = new BackupCommand(
            () => _ = ExportAsync(CancellationToken.None),
            () => !IsRunning);
        _cancelCommand = new BackupCommand(Cancel, () => IsRunning);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand CreateCopyCommand => _createCopyCommand;

    public ICommand ExportCommand => _exportCommand;

    public ICommand CancelCommand => _cancelCommand;

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsIdle));

                // A button bound to a command asks once and then waits to be told. Without this the
                // cancel button would never become usable, however long the copy ran.
                _createCopyCommand.RaiseCanExecuteChanged();
                _exportCommand.RaiseCanExecuteChanged();
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIdle => !IsRunning;

    /// <summary>A resource key rather than a sentence, so the screen speaks the chosen language.</summary>
    public string StatusKey
    {
        get => _statusKey;
        private set
        {
            SetField(ref _statusKey, value);
            OnPropertyChanged(nameof(HasFailed));
        }
    }

    /// <summary>
    /// True for the two outcomes that are failures, and false for the one that only looks like one.
    /// </summary>
    /// <remarks>
    /// Cancelled is not here on purpose: its own words say nothing was left half-written, and somebody
    /// who pressed cancel does not need to be told in red that what they stopped stopped. Derived
    /// rather than set, so a third failure cannot appear without passing through here.
    /// </remarks>
    public bool HasFailed => StatusKey is "BackupStatusFailed" or "BackupStatusNoSpace";

    // The derived flags are announced without asking whether the value moved. Guarding that would add
    // a branch nothing can take for StatusKey - every run passes through "running", so the same key is
    // never assigned twice in a row - and a redundant PropertyChanged on a bool bound to IsVisible
    // costs nothing. A branch nobody can reach is the shape this repository has removed twice already.

    public string? StageKey
    {
        get => _stageKey;
        private set => SetField(ref _stageKey, value);
    }

    public int Completed
    {
        get => _completed;
        private set => SetField(ref _completed, value);
    }

    public int Total
    {
        get => _total;
        private set => SetField(ref _total, value);
    }

    /// <summary>The name of the copy folder, never the path that leads to it.</summary>
    public string? LastCopyName
    {
        get => _lastCopyName;
        private set
        {
            SetField(ref _lastCopyName, value);
            OnPropertyChanged(nameof(HasLastCopy));
            OnPropertyChanged(nameof(HasNoHistory));
        }
    }

    /// <summary>The name of the archive file, never the folder it was written into.</summary>
    public string? LastArchiveName
    {
        get => _lastArchiveName;
        private set
        {
            SetField(ref _lastArchiveName, value);
            OnPropertyChanged(nameof(HasLastArchive));
            OnPropertyChanged(nameof(HasNoHistory));
        }
    }

    /// <summary>
    /// Whether there is a copy to name, and whether there is an export to name.
    /// </summary>
    /// <remarks>
    /// Both names were shaped for a screen from the day they were written - "the name, never the path"
    /// - and no view painted either, so somebody who had made a copy could only find out by going to
    /// look in the file manager. These say when there is something to show.
    /// </remarks>
    /// <summary>
    /// Where the active database lives, so what the recovery screen says is reachable in normal
    /// operation - §4's one addition to this view. Null in a composition that has no host paths,
    /// and the block simply is not painted then.
    /// </summary>
    public string? DatabasePath { get; }

    public bool HasDatabasePath => !string.IsNullOrWhiteSpace(DatabasePath);

    /// <summary>The empty history said in positive terms, not as a blank under two absent names.</summary>
    public bool HasNoHistory => !HasLastCopy && !HasLastArchive;

    public bool HasLastCopy => !string.IsNullOrWhiteSpace(LastCopyName);

    public bool HasLastArchive => !string.IsNullOrWhiteSpace(LastArchiveName);

    public async Task CreateCopyAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBegin(cancellationToken, out var cancellation))
        {
            return;
        }

        try
        {
            var result = await _createCopy(_progress, cancellation.Token).ConfigureAwait(true);
            LastCopyName = Path.GetFileName(result.Copy.Path.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));
            StatusKey = "BackupStatusDone";
        }
        catch (Exception exception)
        {
            StatusKey = Describe(exception);
        }
        finally
        {
            Complete();
        }
    }

    public async Task ExportAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        var destination = await _chooseDestination(cancellationToken).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(destination))
        {
            return;
        }

        if (!TryBegin(cancellationToken, out var cancellation))
        {
            return;
        }

        try
        {
            var result = await _export(destination, _progress, cancellation.Token).ConfigureAwait(true);
            LastArchiveName = Path.GetFileName(result.ArchivePath);
            StatusKey = "BackupStatusDone";
        }
        catch (Exception exception)
        {
            StatusKey = Describe(exception);
        }
        finally
        {
            Complete();
        }
    }

    private static string Describe(Exception exception) => exception switch
    {
        OperationCanceledException => "BackupStatusCancelled",
        InsufficientBackupSpaceException => "BackupStatusNoSpace",
        _ => "BackupStatusFailed",
    };

    private static string StageResourceKey(BackupStage stage) => stage switch
    {
        BackupStage.Snapshot => "BackupStageSnapshot",
        BackupStage.Preferences => "BackupStagePreferences",
        BackupStage.PersonalArtwork => "BackupStagePersonalArtwork",
        BackupStage.Manifest => "BackupStageManifest",
        BackupStage.Publish => "BackupStagePublish",
        _ => "BackupStageArchive",
    };

    private bool TryBegin(CancellationToken cancellationToken, out CancellationTokenSource cancellation)
    {
        cancellation = null!;
        if (IsRunning)
        {
            return false;
        }

        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cancellation = _cancellation;
        Completed = 0;
        Total = 0;
        StageKey = null;
        StatusKey = "BackupStatusRunning";
        IsRunning = true;
        return true;
    }

    private void Complete()
    {
        IsRunning = false;
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private void Cancel() => _cancellation?.Cancel();

    private void Apply(BackupProgress progress)
    {
        StageKey = StageResourceKey(progress.Stage);
        Completed = progress.Completed;
        Total = progress.Total;
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
    /// Reports on the thread that produced the step. <see cref="Progress{T}"/> would post the callback
    /// elsewhere, and a progress bar that arrives after the operation finished is not progress.
    /// </summary>
    private sealed class ImmediateProgress(Action<BackupProgress> report) : IProgress<BackupProgress>
    {
        public void Report(BackupProgress value) => report(value);
    }

    private sealed class BackupCommand(Action execute, Func<bool> canExecute) : ICommand
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
