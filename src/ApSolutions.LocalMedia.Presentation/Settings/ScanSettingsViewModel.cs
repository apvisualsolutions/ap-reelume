using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApSolutions.LocalMedia.Presentation.Settings;

public sealed class ScanSettingsViewModel : INotifyPropertyChanged
{
    private bool _watchLocalRoots = true;
    private int _fallbackIntervalMinutes = 30;

    public event PropertyChangedEventHandler? PropertyChanged;

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

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
