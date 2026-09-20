// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// The scanning group of Settings: whether local folders are followed live, and how long the
/// recovery sweep waits between passes.
/// </summary>
/// <remarks>
/// <b>Both controls were painted, toggled and reset long before either governed anything</b> — two
/// fields on this class with no store behind them and no reader anywhere in <c>src/</c>, which is
/// ENG-010. They read and write the port now, with no field of their own on purpose: a field that
/// remembers its own value looks exactly like one the application reads, and that is how this got
/// through a redesign, a walk and a UX-010 audit without anybody noticing.
/// </remarks>
public sealed class ScanSettingsViewModel : INotifyPropertyChanged
{
    /// <summary>Watching local roots is on to begin with, and is what UX-010's reset puts back.</summary>
    public const bool DefaultWatchLocalRoots = true;

    /// <summary>
    /// What UX-010's reset puts back, taken from the one place the interval lives rather than
    /// written again here. This constant used to say thirty while the sweep ran every fifteen, and
    /// nobody could notice because the screen governed nothing (ENG-044).
    /// </summary>
    public static readonly int DefaultFallbackIntervalMinutes =
        (int)ScanWatchPolicy.DefaultSweepInterval.TotalMinutes;

    private readonly IScanWatchSettings _settings;

    public ScanSettingsViewModel(IScanWatchSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The «Restaurar valores por defecto» of this group (UX-010).</summary>
    public ICommand RestoreDefaultsCommand { get; }

    public bool WatchLocalRoots
    {
        get => _settings.WatchLocalRoots;
        set
        {
            // Writing a value the store already holds would restart every watcher for nothing,
            // because that is what changing this setting now does.
            if (value == _settings.WatchLocalRoots)
            {
                return;
            }

            _settings.SetWatchLocalRoots(value);
            OnPropertyChanged();
        }
    }

    public int FallbackIntervalMinutes
    {
        get => (int)_settings.SweepInterval.TotalMinutes;
        set
        {
            if (value == FallbackIntervalMinutes)
            {
                return;
            }

            _settings.SetSweepInterval(TimeSpan.FromMinutes(value));
            OnPropertyChanged();
        }
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
