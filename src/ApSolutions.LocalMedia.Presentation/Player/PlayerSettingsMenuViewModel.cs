// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The gear over the picture: a list of the groups decided while watching, and the group that
/// replaces that list when one is chosen (ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// <b>Two levels where the second replaces the first</b>, which is the shape of YouTube's menu and
/// the reason the panel fits over a picture at all: a list plus an open group stacked together is
/// twice the height, and the picture is what somebody is trying to look at.
/// </para>
/// <para>
/// <b>Drawn and never floated.</b> Nothing inside a <c>Flyout</c> can be pressed by the autonomous
/// walk, because a flyout's content lands in a popup root of its own — which is why every entry of
/// <c>eng/walk-pending.txt</c> is a flyout child. Putting the player's options in one would take all
/// of them out of that coverage at a stroke.
/// </para>
/// <para>
/// It opens on the list every time rather than where it was left, which is the grammar the player's
/// own pills already use: pressing the gear again gives the picture back.
/// </para>
/// </remarks>
public sealed class PlayerSettingsMenuViewModel : INotifyPropertyChanged
{
    private bool _isOpen;
    private bool _loaded;
    private PlayerSettingsGroup _group = PlayerSettingsGroup.None;

    public PlayerSettingsMenuViewModel(
        PictureAdjustmentViewModel? picture = null,
        Settings.PlaybackSettingsViewModel? nextEpisode = null,
        Settings.SegmentDetectionSettingsViewModel? segments = null,
        SubtitleStyleViewModel? subtitles = null)
    {
        Picture = picture;
        NextEpisode = nextEpisode;
        Segments = segments;
        Subtitles = subtitles;
        ToggleCommand = new MenuCommand(_ => IsOpen = !_isOpen);
        CloseCommand = new MenuCommand(_ => IsOpen = false);
        BackCommand = new MenuCommand(_ => Group = PlayerSettingsGroup.None);
        OpenGroupCommand = new MenuCommand(parameter =>
        {
            if (parameter is PlayerSettingsGroup group)
            {
                Group = group;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The picture group's own model; absent in a session that has no picture to adjust.</summary>
    public PictureAdjustmentViewModel? Picture { get; }

    /// <summary>
    /// The next-episode countdown, down from Settings on 2026-09-13 (ADR-0012). It keeps writing the
    /// global row rather than this session's: ten seconds is an answer for the whole library, and
    /// making it per-series would be a change to the stored model that nobody asked for.
    /// </summary>
    public Settings.PlaybackSettingsViewModel? NextEpisode { get; }

    /// <summary>Automatic segment detection, down from Settings the same day and also global.</summary>
    public Settings.SegmentDetectionSettingsViewModel? Segments { get; }

    /// <summary>The subtitle style, down from Settings the same day and also global.</summary>
    public SubtitleStyleViewModel? Subtitles { get; }

    public ICommand ToggleCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand OpenGroupCommand { get; }

    /// <summary>
    /// Shuts the gear from outside it, which is what the picture going into the small window does:
    /// the bar that carries the button stands down there, so a panel left open would be 380 px of
    /// options over 480 px of picture with no way back to the list.
    /// </summary>
    public void Close() => IsOpen = false;

    public bool IsOpen
    {
        get => _isOpen;
        private set
        {
            if (_isOpen == value)
            {
                return;
            }

            _isOpen = value;

            // Opening is what reads the stored values, and it is here rather than in whoever builds
            // this because a call somebody has to remember is a call somebody forgets: the gear was
            // written with no load at all, and it opened at neutral over a film the engine was
            // already adjusting — three controls disagreeing with the picture, and the first touch
            // writing that neutral over what had been chosen. Once per gear, because the row does
            // not change underneath it.
            if (_isOpen && !_loaded)
            {
                _loaded = true;
                _ = Picture?.LoadAsync();

                // And the subtitle style, which came down from Settings on 2026-09-13 and arrived
                // with the same hole: the page it left was loaded by the window's configuration, and
                // the gear is configured by nobody. The walk caught it — «the style stored before
                // this window opened never reached the screen it belongs to» — which is the second
                // time this exact defect has been found inside this menu.
                _ = Subtitles?.LoadAsync();
            }

            // Closing forgets which group was open, so the next opening starts at the list. Keeping
            // it would hand somebody the inside of a group they did not ask for twice in a row.
            if (!_isOpen)
            {
                _group = PlayerSettingsGroup.None;
            }

            RaiseEverything();
        }
    }

    public PlayerSettingsGroup Group
    {
        get => _group;
        private set
        {
            if (_group == value)
            {
                return;
            }

            _group = value;
            RaiseEverything();
        }
    }

    /// <summary>The first level, which stands only while no group has replaced it.</summary>
    public bool IsListVisible => _isOpen && _group is PlayerSettingsGroup.None;

    /// <summary>The picture group, which stands in place of the list.</summary>
    public bool IsPictureVisible => _isOpen && _group is PlayerSettingsGroup.Picture;

    /// <inheritdoc cref="IsPictureVisible"/>
    public bool IsNextEpisodeVisible => _isOpen && _group is PlayerSettingsGroup.NextEpisode;

    /// <inheritdoc cref="IsPictureVisible"/>
    public bool IsSegmentsVisible => _isOpen && _group is PlayerSettingsGroup.Segments;

    /// <inheritdoc cref="IsPictureVisible"/>
    public bool IsSubtitlesVisible => _isOpen && _group is PlayerSettingsGroup.Subtitles;

    /// <summary>Whether the gear has the countdown group to offer at all.</summary>
    public bool HasNextEpisode => NextEpisode is not null;

    /// <inheritdoc cref="HasNextEpisode"/>
    public bool HasSegments => Segments is not null;

    /// <inheritdoc cref="HasNextEpisode"/>
    public bool HasSubtitles => Subtitles is not null;

    private void RaiseEverything()
    {
        foreach (var name in new[]
        {
            nameof(IsOpen),
            nameof(Group),
            nameof(IsListVisible),
            nameof(IsPictureVisible),
            nameof(IsNextEpisodeVisible),
            nameof(IsSegmentsVisible),
            nameof(IsSubtitlesVisible),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class MenuCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
