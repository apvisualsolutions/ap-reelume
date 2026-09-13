// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

using ApSolutions.LocalMedia.Application.Continuity;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// The switch for automatic segment detection. It is off until a person turns it on, and turning it
/// off is immediate: nothing is extracted or compared while the switch is off.
/// </summary>
public sealed class SegmentDetectionSettingsViewModel : INotifyPropertyChanged
{
    private readonly Func<bool> _readEnabled;
    private readonly Action<bool> _writeEnabled;

    /// <summary>
    /// The factory value this group's «Restaurar valores por defecto» puts back (UX-010). It is the
    /// use case's own constant rather than a second copy of the word «false».
    /// </summary>
    public const bool DefaultEnabled = DetectSeriesSegments.EnabledByDefault;

    public SegmentDetectionSettingsViewModel(Func<bool> readEnabled, Action<bool> writeEnabled)
    {
        _readEnabled = readEnabled ?? throw new ArgumentNullException(nameof(readEnabled));
        _writeEnabled = writeEnabled ?? throw new ArgumentNullException(nameof(writeEnabled));
        RestoreDefaultsCommand = new RelayCommand(() => IsEnabled = DefaultEnabled);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The «Restaurar valores por defecto» of this group (UX-010).</summary>
    /// <remarks>
    /// Always executable, where the picture group offers its own only while something is away from
    /// neutral: this group is one switch, so «there is nothing to undo» and «it is already off» are
    /// the same state, and hiding the button there would leave it unpressable half the time for no
    /// gain. Setting what is already stored writes nothing — the setter sees to that.
    /// </remarks>
    public ICommand RestoreDefaultsCommand { get; }

    public bool IsEnabled
    {
        get => _readEnabled();
        set
        {
            if (_readEnabled() == value)
            {
                return;
            }

            _writeEnabled(value);
            OnPropertyChanged();
        }
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
