using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using Avalonia.Input;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>One row of the shortcut editor.</summary>
public sealed record ShortcutRow(
    PlaybackInputCommand Command,
    string CommandLabel,
    string GestureLabel,
    bool IsCustomised);

/// <summary>
/// Presents and edits the keyboard map. A rebind that would collide is refused immediately and the
/// interface names the command that already holds the gesture, so a conflict is never stored.
/// </summary>
public sealed class ShortcutSettingsViewModel : INotifyPropertyChanged
{
    private readonly ShortcutMap _map;
    private string? _conflictMessage;

    // The map is demanded, never defaulted: an editor with a "?? new" fallback can silently edit a
    // second map that no key press ever reads (ARQ-002).
    public ShortcutSettingsViewModel(ShortcutMap map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ShortcutRow> Bindings { get; } = [];

    public ICommand RestoreDefaultsCommand { get; }

    public bool HasConflict => _conflictMessage is not null;

    public string ConflictMessage => _conflictMessage ?? string.Empty;

    /// <summary>Label used when the interface has no localised name for a command.</summary>
    public static string Describe(PlaybackInputCommand command) => command switch
    {
        PlaybackInputCommand.PlayPause => "Reproducir o pausar",
        PlaybackInputCommand.Stop => "Detener",
        PlaybackInputCommand.SkipBackward => "Retroceder",
        PlaybackInputCommand.SkipForward => "Avanzar",
        PlaybackInputCommand.VolumeUp => "Subir volumen",
        PlaybackInputCommand.VolumeDown => "Bajar volumen",
        PlaybackInputCommand.ToggleMute => "Silenciar",
        PlaybackInputCommand.ToggleFullscreen => "Pantalla completa",
        PlaybackInputCommand.ToggleMiniPlayer => "Mini reproductor",
        _ => "Salir del modo actual",
    };

    /// <summary>Rebinds a command, refusing and reporting a collision instead of storing one.</summary>
    public bool TryRebind(PlaybackInputCommand command, KeyGesture gesture)
    {
        var holder = _map.TryRebind(command, gesture);
        if (holder is { } existing)
        {
            _conflictMessage = $"{gesture} ya está asignado a «{Describe(existing)}».";
            OnPropertyChanged(nameof(HasConflict));
            OnPropertyChanged(nameof(ConflictMessage));
            return false;
        }

        _conflictMessage = null;
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(ConflictMessage));
        Refresh();
        return true;
    }

    /// <summary>The command a gesture triggers right now.</summary>
    public PlaybackInputCommand? Resolve(KeyGesture gesture) => _map.Resolve(gesture);

    private void RestoreDefaults()
    {
        _map.RestoreDefaults();
        _conflictMessage = null;
        OnPropertyChanged(nameof(HasConflict));
        OnPropertyChanged(nameof(ConflictMessage));
        Refresh();
    }

    private void Refresh()
    {
        Bindings.Clear();
        foreach (var binding in _map.Snapshot())
        {
            Bindings.Add(new ShortcutRow(
                binding.Command,
                Describe(binding.Command),
                binding.Gesture.ToString(),
                !binding.IsDefault));
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
