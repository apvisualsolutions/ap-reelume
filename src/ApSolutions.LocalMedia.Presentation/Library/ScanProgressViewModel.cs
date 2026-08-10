// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Events;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed class ScanProgressViewModel : INotifyPropertyChanged
{
    private CancellationTokenSource? _cancellation;
    private int _enumeratedCount;
    private int _probeCount;
    private string? _currentPath;
    private bool _isRunning;

    public ScanProgressViewModel()
    {
    }

    public ScanProgressViewModel(InProcessApplicationEventPublisher eventPublisher)
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

    public void Begin(CancellationTokenSource cancellation)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        _cancellation = cancellation;
        EnumeratedCount = 0;
        ProbeCount = 0;
        CurrentPath = null;
        IsRunning = true;
        OnPropertyChanged(nameof(CanCancel));
    }

    public void Apply(ScanProgressChanged progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        EnumeratedCount = progress.EnumeratedCount;
        ProbeCount = progress.ProbeCount;
        CurrentPath = progress.CurrentPath;
        if (progress.IsCompleted)
        {
            IsRunning = false;
            _cancellation = null;
            OnPropertyChanged(nameof(CanCancel));
        }
    }

    public void Cancel()
    {
        if (!CanCancel)
        {
            return;
        }

        _cancellation?.Cancel();
        OnPropertyChanged(nameof(CanCancel));
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
