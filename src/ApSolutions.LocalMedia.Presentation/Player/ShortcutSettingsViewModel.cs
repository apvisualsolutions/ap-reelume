// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation.Courses;
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
        Bindings.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
        _map = map ?? throw new ArgumentNullException(nameof(map));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ShortcutRow> Bindings { get; } = [];

    /// <summary>
    /// Nothing is bound to a key, which is not the same as the application ignoring the keyboard.
    /// </summary>
    /// <remarks>
    /// Derived from the list and announced by the list: a row can arrive or leave from more than one
    /// path, and one that forgot to announce would leave the panel saying it is empty over a list with
    /// shortcuts in it.
    /// </remarks>
    public bool IsEmpty => Bindings.Count == 0;

    public ICommand RestoreDefaultsCommand { get; }

    public bool HasConflict => _conflictMessage is not null;

    public string ConflictMessage => _conflictMessage ?? string.Empty;

    /// <summary>What a command is called, in the language the application is speaking.</summary>
    /// <remarks>
    /// <b>These were ten Spanish literals until 2026-09-02</b>, so the shortcut list read in Spanish
    /// with the application set to English — every row of it. Nothing caught it because the bilingual
    /// gates read the views' markup, and a visible string that lives in a <c>.cs</c> file is outside
    /// what they look at.
    /// <para>
    /// The fallback beside each key is the English text rather than a placeholder: a resource that
    /// fails to resolve should leave a readable row, and this method's answer is a label a person
    /// reads.
    /// </para>
    /// </remarks>
    public static string Describe(PlaybackInputCommand command) => command switch
    {
        PlaybackInputCommand.PlayPause => CourseText.Resource("ShortcutCommandPlayPause", "Play or pause"),
        PlaybackInputCommand.Stop => CourseText.Resource("ShortcutCommandStop", "Stop"),
        PlaybackInputCommand.SkipBackward => CourseText.Resource("ShortcutCommandSkipBackward", "Skip backward"),
        PlaybackInputCommand.SkipForward => CourseText.Resource("ShortcutCommandSkipForward", "Skip forward"),
        PlaybackInputCommand.VolumeUp => CourseText.Resource("ShortcutCommandVolumeUp", "Volume up"),
        PlaybackInputCommand.VolumeDown => CourseText.Resource("ShortcutCommandVolumeDown", "Volume down"),
        PlaybackInputCommand.ToggleMute => CourseText.Resource("ShortcutCommandToggleMute", "Mute"),
        PlaybackInputCommand.ToggleFullscreen => CourseText.Resource("ShortcutCommandToggleFullscreen", "Full screen"),
        PlaybackInputCommand.ToggleMiniPlayer => CourseText.Resource("ShortcutCommandToggleMiniPlayer", "Mini player"),
        _ => CourseText.Resource("ShortcutCommandExitMode", "Leave the current mode"),
    };


    /// <summary>Rebinds a command, refusing and reporting a collision instead of storing one.</summary>
    public bool TryRebind(PlaybackInputCommand command, KeyGesture gesture)
    {
        var holder = _map.TryRebind(command, gesture);
        if (holder is { } existing)
        {
            // The sentence is a resource with two holes rather than an interpolation, because the
            // words around them differ per language and the order of the two could too.
            //
            // Parsed here and not cached, which is what CA1863 asks for and what this case cannot
            // give: the format IS the language, and a cached one would keep the sentence of whatever
            // language happened to be in force the first time somebody hit a key collision.
            var format = CompositeFormat.Parse(
                CourseText.Resource("ShortcutConflictFormat", "{0} is already assigned to \"{1}\"."));
            _conflictMessage = string.Format(
                CultureInfo.CurrentCulture,
                format,
                gesture,
                Describe(existing));
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
