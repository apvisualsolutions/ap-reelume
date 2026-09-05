// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// What the Library says while a scan runs, and it says it two ways (ADR-0010).
/// </summary>
/// <remarks>
/// A scan somebody launched by hand gets the strip: it describes a state, so it takes space and
/// pushes the grid down, and it carries the cancel button. A scan that starts on its own gets the
/// pulse in the header instead, where the space is already spent — the ADR's fifth point is that a
/// shift nobody asked for is not paid for, and that is the whole difference between the two.
/// <para>
/// Until 2026-09-05 the second case drew NOTHING. <c>Begin</c> is the only thing that ever set
/// <c>IsRunning</c>, and only the hand-launched route calls it, so a startup or watcher scan sent its
/// progress into a surface that stayed invisible. <c>Apply</c> lights it now, and cancelling stays
/// with <c>Begin</c> because only the caller that owns the token can offer to stop it.
/// </para>
/// </remarks>
public sealed class ScanProgressViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand _cancel;
    private CancellationTokenSource? _cancellation;
    private int _enumeratedCount;
    private int _probeCount;
    private string? _currentPath;
    private bool _isRunning;
    private bool _hasFinished;
    private ScanTrigger _trigger = ScanTrigger.Manual;
    private bool _announcedCanCancel;
    private bool _announcedShowsStrip;
    private bool _announcedShowsPulse;

    public ScanProgressViewModel() => _cancel = new RelayCommand(_ => Cancel(), _ => CanCancel);

    public ScanProgressViewModel(InProcessApplicationEventPublisher eventPublisher)
        : this()
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);
        eventPublisher.Published += applicationEvent =>
        {
            if (applicationEvent is ScanProgressChanged progress)
            {
                Apply(progress);
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int EnumeratedCount
    {
        get => _enumeratedCount;
        private set => SetField(ref _enumeratedCount, value);
    }

    public int ProbeCount
    {
        get => _probeCount;
        private set => SetField(ref _probeCount, value);
    }

    public string? CurrentPath
    {
        get => _currentPath;
        private set => SetField(ref _currentPath, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetField(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanCancel));
            }
        }
    }

    public bool CanCancel => IsRunning && _cancellation is { IsCancellationRequested: false };

    /// <summary>
    /// The strip, which is what a scan a person asked for gets. Initial is here beside Manual because
    /// it is the scan that follows granting a folder, and the person is looking straight at it.
    /// </summary>
    public bool ShowsStrip => IsRunning && _trigger is ScanTrigger.Initial or ScanTrigger.Manual;

    /// <summary>The pulse beside the count, which is what a scan nobody asked for gets.</summary>
    public bool ShowsPulse => IsRunning && !ShowsStrip;

    /// <summary>
    /// The one line the row used to skip: it vanished instead of saying it was done, while the words
    /// for saying so sat translated in both dictionaries with no reader.
    /// </summary>
    public bool HasFinished
    {
        get => _hasFinished;
        private set => SetField(ref _hasFinished, value);
    }

    /// <summary>Stops the scan. Its guard reads state that moves, so it announces (ARQ-004).</summary>
    public ICommand CancelCommand => _cancel;

    public void Begin(CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        _cancellation = cancellation;
        EnumeratedCount = 0;
        ProbeCount = 0;
        CurrentPath = null;
        HasFinished = false;
        _trigger = ScanTrigger.Manual;
        IsRunning = true;
        AnnounceCancellation();
    }

    public void Apply(ScanProgressChanged progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        EnumeratedCount = progress.EnumeratedCount;
        ProbeCount = progress.ProbeCount;
        CurrentPath = progress.CurrentPath;

        // The trigger only moves while a scan is live. Reading it from the completion event too would
        // let the last message of a hand-launched scan relabel the pulse of the next automatic one.
        if (!progress.IsCompleted)
        {
            _trigger = progress.Trigger;
            HasFinished = false;
            IsRunning = true;
        }
        else
        {
            IsRunning = false;
            HasFinished = true;
            _cancellation = null;
        }

        AnnounceCancellation();
    }

    public void Cancel()
    {
        if (!CanCancel)
        {
            return;
        }

        _cancellation?.Cancel();
        AnnounceCancellation();
    }

    /// <summary>
    /// Says what actually moved, and nothing else.
    /// </summary>
    /// <remarks>
    /// <b>Measured on 2026-09-05, and it is why this is not three unconditional calls.</b> A scan
    /// publishes progress once per batch, and <c>Apply</c> runs on the scanning thread. Announcing
    /// <c>CanExecuteChanged</c> every time put a button's enabled-state recalculation on the
    /// interface thread hundreds of times a second, and the walk's watcher scene went from 4 seconds
    /// to timing out at 63 while the catalogue waited behind the flood — the copies were catalogued
    /// and never grouped.
    /// <para>
    /// Bisected to this line: with the button's <c>Command</c> binding removed the same scene passed
    /// in 4 seconds again. Nothing was throwing; the interface thread was simply never getting to the
    /// work. A guard that says nothing when nothing changed costs three comparisons and is the honest
    /// answer anyway — during an automatic scan none of these three ever move.
    /// </para>
    /// </remarks>
    private void AnnounceCancellation()
    {
        if (_announcedCanCancel != CanCancel)
        {
            _announcedCanCancel = CanCancel;
            OnPropertyChanged(nameof(CanCancel));
            _cancel.RaiseCanExecuteChanged();
        }

        if (_announcedShowsStrip != ShowsStrip)
        {
            _announcedShowsStrip = ShowsStrip;
            OnPropertyChanged(nameof(ShowsStrip));
        }

        if (_announcedShowsPulse != ShowsPulse)
        {
            _announcedShowsPulse = ShowsPulse;
            OnPropertyChanged(nameof(ShowsPulse));
        }
    }

    private sealed class RelayCommand(Action<object?> execute, Predicate<object?> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter) => execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
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
}
