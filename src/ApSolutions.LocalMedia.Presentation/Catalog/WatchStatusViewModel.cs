// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Presentation.Catalog;

/// <summary>
/// Shows whether content is unwatched, under way, or finished, and lets the person say so directly.
/// The state is presented as an icon and words rather than colour alone, and a manual decision is
/// announced in text so it is never a silent difference.
/// </summary>
public sealed class WatchStatusViewModel : INotifyPropertyChanged
{
    private readonly Func<WatchStatus?, Task>? _onChanged;
    private WatchStatus _status = WatchStatus.NotStarted;
    private bool _isManualOverride;

    public WatchStatusViewModel(Func<WatchStatus?, Task>? onChanged = null)
    {
        _onChanged = onChanged;
        MarkWatchedCommand = new StatusCommand(() => RequestAsync(WatchStatus.Watched));
        MarkNotStartedCommand = new StatusCommand(() => RequestAsync(WatchStatus.NotStarted));
        ClearOverrideCommand = new StatusCommand(() => RequestAsync(null), () => IsManualOverride);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand MarkWatchedCommand { get; }

    public ICommand MarkNotStartedCommand { get; }

    public ICommand ClearOverrideCommand { get; }

    public WatchStatus Status => _status;

    public bool IsNotStarted => _status == WatchStatus.NotStarted;

    public bool IsInProgress => _status == WatchStatus.InProgress;

    public bool IsWatched => _status == WatchStatus.Watched;

    /// <summary>True while a decision made by hand is in force.</summary>
    public bool IsManualOverride => _isManualOverride;

    /// <summary>Applies what the repository holds; the control never decides the state itself.</summary>
    public void Apply(WatchStatus status, bool isManualOverride)
    {
        _status = status;
        _isManualOverride = isManualOverride;
        foreach (var name in new[]
        {
            nameof(Status),
            nameof(IsNotStarted),
            nameof(IsInProgress),
            nameof(IsWatched),
            nameof(IsManualOverride),
        })
        {
            OnPropertyChanged(name);
        }

        (ClearOverrideCommand as StatusCommand)?.RaiseCanExecuteChanged();
    }

    private Task RequestAsync(WatchStatus? status) => _onChanged?.Invoke(status) ?? Task.CompletedTask;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class StatusCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

        public async void Execute(object? parameter) => await execute().ConfigureAwait(true);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
