// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Domain.Lifecycle;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// The consent surface for the tray and for starting with Windows.
/// <para>
/// Asking for automatic start does not grant it: the request is held until the person confirms it on
/// this screen, and only then does anything reach the registry. Withdrawing it needs no confirmation,
/// because taking a permission back must never be harder than giving it.
/// </para>
/// </summary>
public sealed class LifecycleSettingsViewModel : INotifyPropertyChanged
{
    private readonly ILifecycleSettings _settings;
    private readonly IStartupService _startup;
    private LifecyclePreferences _preferences;
    private bool _isStartupConsentPending;

    public LifecycleSettingsViewModel(ILifecycleSettings settings, IStartupService startup)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _startup = startup ?? throw new ArgumentNullException(nameof(startup));
        _preferences = AppLifecyclePolicy.Normalize(_settings.Current);
        GrantStartupConsentCommand = new LifecycleCommand(GrantStartupConsent);
        DeclineStartupConsentCommand = new LifecycleCommand(DeclineStartupConsent);

        // A registration that points somewhere else would start the wrong thing at sign-in, so it is
        // corrected as soon as the screen that owns it is built.
        if (_preferences.StartWithWindows)
        {
            _startup.Repair();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand GrantStartupConsentCommand { get; }

    public ICommand DeclineStartupConsentCommand { get; }

    public bool TrayEnabled
    {
        get => _preferences.TrayEnabled;
        set
        {
            if (_preferences.TrayEnabled == value)
            {
                return;
            }

            Apply(AppLifecyclePolicy.WithTray(_preferences, value));
        }
    }

    public bool StartWithWindows
    {
        get => _preferences.StartWithWindows;
        set
        {
            if (_preferences.StartWithWindows == value)
            {
                return;
            }

            if (value)
            {
                IsStartupConsentPending = true;
                OnPropertyChanged(nameof(StartWithWindows));
                return;
            }

            _startup.Disable();
            IsStartupConsentPending = false;
            Apply(AppLifecyclePolicy.WithStartup(_preferences, isRequested: false, hasConsent: false));
        }
    }

    public bool MinimizeToTrayOnClose
    {
        get => _preferences.CloseBehavior == CloseBehavior.MinimizeToTray;
        set
        {
            var behavior = value ? CloseBehavior.MinimizeToTray : CloseBehavior.Exit;
            if (_preferences.CloseBehavior == behavior)
            {
                return;
            }

            Apply(AppLifecyclePolicy.WithCloseBehavior(_preferences, behavior));
        }
    }

    /// <summary>Closing to the tray is only offered while the tray exists.</summary>
    public bool CanMinimizeToTray => _preferences.TrayEnabled;

    /// <summary>True while the request is waiting for the person to confirm it.</summary>
    public bool IsStartupConsentPending
    {
        get => _isStartupConsentPending;
        private set
        {
            if (_isStartupConsentPending == value)
            {
                return;
            }

            _isStartupConsentPending = value;
            OnPropertyChanged();
        }
    }

    public bool TrayIsDisabled => !TrayEnabled;

    public bool StartupIsDisabled => !StartWithWindows;

    private void GrantStartupConsent()
    {
        if (!IsStartupConsentPending)
        {
            return;
        }

        _startup.Enable();
        IsStartupConsentPending = false;
        Apply(AppLifecyclePolicy.WithStartup(_preferences, isRequested: true, hasConsent: true));
    }

    private void DeclineStartupConsent()
    {
        IsStartupConsentPending = false;
        OnPropertyChanged(nameof(StartWithWindows));
    }

    private void Apply(LifecyclePreferences updated)
    {
        _preferences = AppLifecyclePolicy.Normalize(updated);
        _settings.Save(_preferences);
        OnPropertyChanged(nameof(TrayEnabled));
        OnPropertyChanged(nameof(TrayIsDisabled));
        OnPropertyChanged(nameof(StartWithWindows));
        OnPropertyChanged(nameof(StartupIsDisabled));
        OnPropertyChanged(nameof(MinimizeToTrayOnClose));
        OnPropertyChanged(nameof(CanMinimizeToTray));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class LifecycleCommand(Action execute) : ICommand
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
