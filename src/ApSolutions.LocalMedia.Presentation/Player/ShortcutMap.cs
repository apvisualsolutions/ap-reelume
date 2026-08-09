using ApSolutions.LocalMedia.Application.Playback;
using Avalonia.Input;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>One command bound to one gesture, and whether the binding is the shipped default.</summary>
public sealed record ShortcutBinding(PlaybackInputCommand Command, KeyGesture Gesture, bool IsDefault);

/// <summary>
/// The keyboard map. Every essential action has a default binding, a binding can be changed, and a
/// gesture already in use is refused with the command that holds it, so a conflict is impossible to
/// create by accident.
/// </summary>
public sealed class ShortcutMap
{
    private readonly Dictionary<PlaybackInputCommand, KeyGesture> _bindings;

    public ShortcutMap() => _bindings = new Dictionary<PlaybackInputCommand, KeyGesture>(Defaults);

    /// <summary>The shipped bindings; every command the player exposes has one.</summary>
    public static IReadOnlyDictionary<PlaybackInputCommand, KeyGesture> Defaults { get; } =
        new Dictionary<PlaybackInputCommand, KeyGesture>
        {
            [PlaybackInputCommand.PlayPause] = new(Key.Space),
            [PlaybackInputCommand.Stop] = new(Key.S, KeyModifiers.Control),
            [PlaybackInputCommand.SkipBackward] = new(Key.Left),
            [PlaybackInputCommand.SkipForward] = new(Key.Right),
            [PlaybackInputCommand.VolumeUp] = new(Key.Up),
            [PlaybackInputCommand.VolumeDown] = new(Key.Down),
            [PlaybackInputCommand.ToggleMute] = new(Key.M),
            [PlaybackInputCommand.ToggleFullscreen] = new(Key.F),
            [PlaybackInputCommand.ToggleMiniPlayer] = new(Key.P, KeyModifiers.Control),
            [PlaybackInputCommand.ExitOverlayMode] = new(Key.Escape),
        };

    public IReadOnlyDictionary<PlaybackInputCommand, KeyGesture> Bindings => _bindings;

    /// <summary>The command a gesture triggers, or null when nothing is bound to it.</summary>
    public PlaybackInputCommand? Resolve(KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        foreach (var (command, bound) in _bindings)
        {
            if (bound.Equals(gesture))
            {
                return command;
            }
        }

        return null;
    }

    /// <summary>
    /// Rebinds a command. Returns the command that already holds the gesture when there is a
    /// conflict, and changes nothing in that case.
    /// </summary>
    public PlaybackInputCommand? TryRebind(PlaybackInputCommand command, KeyGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        if (!Enum.IsDefined(command))
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        var holder = Resolve(gesture);
        if (holder is { } existing && existing != command)
        {
            return existing;
        }

        _bindings[command] = gesture;
        return null;
    }

    /// <summary>True when a command still uses the binding it shipped with.</summary>
    public bool IsDefault(PlaybackInputCommand command) =>
        Defaults.TryGetValue(command, out var shipped) && _bindings[command].Equals(shipped);

    /// <summary>Restores every shipped binding.</summary>
    public void RestoreDefaults()
    {
        _bindings.Clear();
        foreach (var (command, gesture) in Defaults)
        {
            _bindings[command] = gesture;
        }
    }

    /// <summary>The current map as a list the interface can present and audit.</summary>
    public IReadOnlyList<ShortcutBinding> Snapshot() =>
        [.. _bindings.Select(pair => new ShortcutBinding(pair.Key, pair.Value, IsDefault(pair.Key)))
            .OrderBy(binding => binding.Command)];
}
