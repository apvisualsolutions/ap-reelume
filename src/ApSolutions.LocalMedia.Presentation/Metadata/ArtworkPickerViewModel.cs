using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApSolutions.LocalMedia.Presentation.Metadata;

public sealed class ArtworkPickerViewModel : INotifyPropertyChanged
{
    private string? _selectedPersonalPath;
    private Uri? _selectedRemoteUri;
    private string? _alternativeText;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? SelectedPersonalPath
    {
        get => _selectedPersonalPath;
        set
        {
            if (SetField(ref _selectedPersonalPath, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public Uri? SelectedRemoteUri
    {
        get => _selectedRemoteUri;
        set
        {
            if (SetField(ref _selectedRemoteUri, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public string? AlternativeText
    {
        get => _alternativeText;
        set
        {
            if (SetField(ref _alternativeText, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public bool CanApply =>
        !string.IsNullOrWhiteSpace(AlternativeText)
        && (!string.IsNullOrWhiteSpace(SelectedPersonalPath) || SelectedRemoteUri is not null);

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
