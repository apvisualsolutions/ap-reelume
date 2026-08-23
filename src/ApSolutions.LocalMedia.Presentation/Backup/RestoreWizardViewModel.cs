// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Domain.Backup;

namespace ApSolutions.LocalMedia.Presentation.Backup;

/// <summary>One reason the wizard is refusing, as a key rather than a sentence.</summary>
public sealed record RestoreFindingViewModel(string MessageKey, string Detail);

/// <summary>
/// The restore wizard. Nothing is replaced until a person has seen what the restore would do and said
/// yes, and the confirmation does not exist while the dry run is refusing.
/// <para>
/// Like the backup screen, this one names files and never the folders that lead to them.
/// </para>
/// </summary>
public sealed class RestoreWizardViewModel : INotifyPropertyChanged
{
    private readonly Func<string, IReadOnlyList<RootRemap>, CancellationToken, Task<RestorePreview>> _preview;
    private readonly Func<string, IReadOnlyList<RootRemap>, IProgress<BackupProgress>, CancellationToken, Task<RestoreResult>> _restore;
    private readonly Func<CancellationToken, Task<string?>> _chooseArchive;
    private readonly IProgress<BackupProgress> _progress;
    private readonly WizardCommand _chooseArchiveCommand;
    private readonly WizardCommand _confirm;
    private string? _archivePath;
    private bool _hasPreview;
    private bool _canRestore;
    private bool _isRunning;
    private string _statusKey = "RestoreStatusIdle";
    private int _mediaFileCount;
    private int _pathChangeCount;
    private string? _preservedDatabaseName;
    private int _completed;
    private int _total;

    public RestoreWizardViewModel(
        Func<string, IReadOnlyList<RootRemap>, CancellationToken, Task<RestorePreview>> preview,
        Func<string, IReadOnlyList<RootRemap>, IProgress<BackupProgress>, CancellationToken, Task<RestoreResult>> restore,
        Func<CancellationToken, Task<string?>> chooseArchive)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _restore = restore ?? throw new ArgumentNullException(nameof(restore));
        _chooseArchive = chooseArchive ?? throw new ArgumentNullException(nameof(chooseArchive));
        _progress = new ImmediateProgress(Apply);
        _chooseArchiveCommand = new WizardCommand(
            () => _ = PreviewAsync(CancellationToken.None),
            () => !IsRunning);
        _confirm = new WizardCommand(
            () => _ = ConfirmAsync(CancellationToken.None),
            () => CanRestore && !IsRunning);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ChooseArchiveCommand => _chooseArchiveCommand;

    public ICommand ConfirmCommand => _confirm;

    public ObservableCollection<RootRemapRowViewModel> Roots { get; } = [];

    /// <summary>
    /// Every folder in the archive exists on this machine: the desirable answer of step two, said
    /// in positive terms instead of an empty gap where questions would be.
    /// </summary>
    public bool HasNothingToRemap => HasPreview && Roots.All(row => !row.NeedsFolder);

    public ObservableCollection<RestoreFindingViewModel> Findings { get; } = [];

    public bool HasPreview
    {
        get => _hasPreview;
        private set
        {
            if (SetField(ref _hasPreview, value))
            {
                OnPropertyChanged(nameof(HasNothingToRemap));
            }
        }
    }

    public bool CanRestore
    {
        get => _canRestore;
        private set
        {
            if (SetField(ref _canRestore, value))
            {
                _confirm.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(IsIdle));

                // Without telling the surface, the confirmation stays disabled for the whole life of
                // the screen and a valid restore can never be started from the application at all.
                _chooseArchiveCommand.RaiseCanExecuteChanged();
                _confirm.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIdle => !IsRunning;

    public string StatusKey
    {
        get => _statusKey;
        private set => SetField(ref _statusKey, value);
    }

    public int MediaFileCount
    {
        get => _mediaFileCount;
        private set => SetField(ref _mediaFileCount, value);
    }

    public int PathChangeCount
    {
        get => _pathChangeCount;
        private set => SetField(ref _pathChangeCount, value);
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

    /// <summary>The name of the database that was moved aside, never the folder holding it.</summary>
    public string? PreservedDatabaseName
    {
        get => _preservedDatabaseName;
        private set => SetField(ref _preservedDatabaseName, value);
    }

    /// <summary>
    /// Asks for an archive the first time and runs the dry run. Later calls reuse the archive already
    /// chosen, which is what makes editing a folder and looking again feel like one screen.
    /// </summary>
    public async Task PreviewAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        if (_archivePath is null)
        {
            var chosen = await _chooseArchive(cancellationToken).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(chosen))
            {
                return;
            }

            _archivePath = chosen;
        }

        IsRunning = true;
        try
        {
            Apply(await _preview(_archivePath, CurrentRemaps(), cancellationToken).ConfigureAwait(true));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            Findings.Clear();
            Findings.Add(new RestoreFindingViewModel(
                $"RestoreFinding{RestoreFindingKind.UnreadableArchive}",
                exception.Message));
            HasPreview = true;
            CanRestore = false;
            StatusKey = "RestoreStatusRefused";
        }
        finally
        {
            IsRunning = false;
        }
    }

    public async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRestore || IsRunning || _archivePath is null)
        {
            return;
        }

        IsRunning = true;
        StatusKey = "RestoreStatusRunning";
        try
        {
            var result = await _restore(_archivePath, CurrentRemaps(), _progress, cancellationToken)
                .ConfigureAwait(true);
            Apply(result.Preview);
            if (result.Restored)
            {
                PreservedDatabaseName = result.PreservedDatabasePath is { } preserved
                    ? Path.GetFileName(preserved)
                    : null;
                CanRestore = false;
                StatusKey = "RestoreStatusRestored";
            }
            else
            {
                PreservedDatabaseName = null;
                StatusKey = "RestoreStatusFailed";
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            PreservedDatabaseName = null;
            StatusKey = "RestoreStatusFailed";
            Findings.Add(new RestoreFindingViewModel(
                $"RestoreFinding{RestoreFindingKind.UnreadableArchive}",
                exception.Message));
        }
        finally
        {
            IsRunning = false;
        }
    }

    private IReadOnlyList<RootRemap> CurrentRemaps() =>
        [.. Roots.Where(row => row.HasRemap).Select(row => new RootRemap(row.OldPath, row.NewPath))];

    private void Apply(RestorePreview preview)
    {
        MergeRoots(preview.Roots);
        OnPropertyChanged(nameof(HasNothingToRemap));
        Findings.Clear();
        foreach (var finding in preview.Findings)
        {
            Findings.Add(new RestoreFindingViewModel($"RestoreFinding{finding.Kind}", finding.Detail));
        }

        MediaFileCount = preview.MediaFileCount;
        PathChangeCount = preview.PathChangeCount;
        HasPreview = true;
        CanRestore = preview.CanRestore;
        StatusKey = preview.CanRestore ? "RestoreStatusPreviewed" : "RestoreStatusRefused";
    }

    /// <summary>
    /// Keeps the rows a person is editing instead of rebuilding them, so a fresh dry run never takes the
    /// caret out of the box somebody is typing in.
    /// </summary>
    private void MergeRoots(IReadOnlyList<RootRemapDecision> decisions)
    {
        foreach (var decision in decisions)
        {
            var existing = Roots.FirstOrDefault(row =>
                row.OldPath.Equals(decision.OldPath, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Roots.Add(new RootRemapRowViewModel(decision));
            }
            else
            {
                existing.Apply(decision);
            }
        }

        for (var index = Roots.Count - 1; index >= 0; index--)
        {
            if (!decisions.Any(decision =>
                decision.OldPath.Equals(Roots[index].OldPath, StringComparison.OrdinalIgnoreCase)))
            {
                Roots.RemoveAt(index);
            }
        }
    }

    private void Apply(BackupProgress progress)
    {
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

    private sealed class ImmediateProgress(Action<BackupProgress> report) : IProgress<BackupProgress>
    {
        public void Report(BackupProgress value) => report(value);
    }

    private sealed class WizardCommand(Action execute, Func<bool> canExecute) : ICommand
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
