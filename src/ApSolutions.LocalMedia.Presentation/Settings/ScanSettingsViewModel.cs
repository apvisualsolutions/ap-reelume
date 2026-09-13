// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ApSolutions.LocalMedia.Presentation.Settings;

public sealed class ScanSettingsViewModel : INotifyPropertyChanged
{
    /// <summary>Watching local roots is on to begin with, and is what UX-010's reset puts back.</summary>
    public const bool DefaultWatchLocalRoots = true;

    /// <summary>Half an hour between fallback sweeps, and what UX-010's reset puts back.</summary>
    public const int DefaultFallbackIntervalMinutes = 30;

    private bool _watchLocalRoots = DefaultWatchLocalRoots;
    private int _fallbackIntervalMinutes = DefaultFallbackIntervalMinutes;

    public ScanSettingsViewModel() =>
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The «Restaurar valores por defecto» of this group (UX-010).</summary>
    public ICommand RestoreDefaultsCommand { get; }

    public bool WatchLocalRoots
    {
        get => _watchLocalRoots;
        set => SetField(ref _watchLocalRoots, value);
    }

    public int FallbackIntervalMinutes
    {
        get => _fallbackIntervalMinutes;
        set => SetField(ref _fallbackIntervalMinutes, value);
    }

    /// <summary>
    /// Puts back every value in the group and not one of them, which is the half a reset written per
    /// control gets wrong: a group half restored looks restored and is not.
    /// </summary>
    private void RestoreDefaults()
    {
        WatchLocalRoots = DefaultWatchLocalRoots;
        FallbackIntervalMinutes = DefaultFallbackIntervalMinutes;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class RelayCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute();
    }
}
