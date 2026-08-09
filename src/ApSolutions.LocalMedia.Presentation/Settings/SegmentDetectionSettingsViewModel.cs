using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// The switch for automatic segment detection. It is off until a person turns it on, and turning it
/// off is immediate: nothing is extracted or compared while the switch is off.
/// </summary>
public sealed class SegmentDetectionSettingsViewModel : INotifyPropertyChanged
{
    private readonly Func<bool> _readEnabled;
    private readonly Action<bool> _writeEnabled;

    public SegmentDetectionSettingsViewModel(Func<bool> readEnabled, Action<bool> writeEnabled)
    {
        _readEnabled = readEnabled ?? throw new ArgumentNullException(nameof(readEnabled));
        _writeEnabled = writeEnabled ?? throw new ArgumentNullException(nameof(writeEnabled));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
}
