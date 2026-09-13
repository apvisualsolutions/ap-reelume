// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The gear's two levels: a list of groups, and the group that replaces it (ADR-0012). Nothing here
/// is a popup, because nothing inside one can be reached by the walk.
/// </summary>
public sealed class PlayerSettingsMenuTests
{
    [Fact]
    public void The_gear_opens_on_the_list_and_a_group_replaces_it()
    {
        var menu = new PlayerSettingsMenuViewModel();

        Assert.False(menu.IsOpen);
        Assert.False(menu.IsListVisible);

        menu.ToggleCommand.Execute(null);

        Assert.True(menu.IsOpen);
        Assert.True(menu.IsListVisible);
        Assert.Equal(PlayerSettingsGroup.None, menu.Group);

        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);

        Assert.True(menu.IsOpen);
        Assert.Equal(PlayerSettingsGroup.Picture, menu.Group);
        // The second level replaces the first rather than sitting beside it, which is the whole
        // shape: two lists on screen at once is a menu twice as tall as the picture allows.
        Assert.False(menu.IsListVisible);
        Assert.True(menu.IsPictureVisible);
    }

    [Fact]
    public void Going_back_returns_to_the_list_without_closing_the_gear()
    {
        var menu = new PlayerSettingsMenuViewModel();
        menu.ToggleCommand.Execute(null);
        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);

        menu.BackCommand.Execute(null);

        Assert.True(menu.IsOpen);
        Assert.True(menu.IsListVisible);
        Assert.False(menu.IsPictureVisible);
    }

    /// <summary>
    /// Pressing the gear again closes it, which is the grammar the player's own pills already use,
    /// and the next opening starts at the list rather than where it was left.
    /// </summary>
    [Fact]
    public void Closing_and_opening_again_starts_at_the_list()
    {
        var menu = new PlayerSettingsMenuViewModel();
        menu.ToggleCommand.Execute(null);
        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);

        menu.ToggleCommand.Execute(null);

        Assert.False(menu.IsOpen);
        Assert.False(menu.IsPictureVisible);
        // Named while it is shut, because that is the only moment the two ways of writing this
        // differ: forgetting the group on the way out, or on the way back in. A gear that is closed
        // and still says it is showing the picture is a state somebody will read one day.
        Assert.Equal(PlayerSettingsGroup.None, menu.Group);

        menu.ToggleCommand.Execute(null);

        Assert.True(menu.IsListVisible);
        Assert.Equal(PlayerSettingsGroup.None, menu.Group);
    }

    /// <summary>
    /// The picture going into the small window shuts the gear, because the bar that carries the
    /// button stands down there: an open panel would sit over 480 px of picture with no way out.
    /// </summary>
    [Fact]
    public void Going_into_the_small_window_shuts_the_gear()
    {
        var menu = new PlayerSettingsMenuViewModel();
        var player = new PlayerViewModel(new SilentCoordinator(), settings: menu);
        menu.ToggleCommand.Execute(null);
        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);

        player.IsCompact = true;

        Assert.False(menu.IsOpen);
        Assert.Equal(PlayerSettingsGroup.None, menu.Group);
    }

    [Fact]
    public void Closing_the_gear_hides_everything_it_was_showing()
    {
        var menu = new PlayerSettingsMenuViewModel();
        menu.ToggleCommand.Execute(null);
        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);

        menu.CloseCommand.Execute(null);

        Assert.False(menu.IsOpen);
        Assert.False(menu.IsListVisible);
        Assert.False(menu.IsPictureVisible);
    }

    /// <summary>
    /// The three ways of asking for the state something is already in, which all answer by doing
    /// nothing: closing what is shut, opening the group that is open, and handing the open command
    /// something that is not a group at all — a `CommandParameter` typo in markup reaches exactly
    /// that last one, and a gear that jumped somewhere on it would be worse than one that sat still.
    /// </summary>
    [Fact]
    public void Asking_for_the_state_it_is_already_in_changes_nothing()
    {
        var menu = new PlayerSettingsMenuViewModel();

        menu.CloseCommand.Execute(null);
        Assert.False(menu.IsOpen);

        menu.ToggleCommand.Execute(null);
        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);
        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);
        Assert.Equal(PlayerSettingsGroup.Picture, menu.Group);

        menu.BackCommand.Execute(null);
        menu.OpenGroupCommand.Execute("Picture");
        Assert.Equal(PlayerSettingsGroup.None, menu.Group);
        Assert.True(menu.IsListVisible);
    }

    /// <summary>
    /// Opening the gear is what reads the stored adjustment, and the panel shows it rather than the
    /// neutral it was built at. Without this the gear opened at neutral over a film the engine was
    /// already adjusting, and the first touch of a control wrote that neutral over what somebody had
    /// chosen — built and never fed, in the one panel whose whole job is to show a stored value.
    /// </summary>
    [Fact]
    public void Opening_the_gear_is_what_reads_what_was_stored()
    {
        var repository = new SilentPreferences
        {
            Stored = new PlaybackPreference
            {
                Scope = PreferenceScope.Global,
                ScopeKey = PlaybackPreference.GlobalKey,
                Picture = new PictureAdjustment(0.2, 1.3, 1.7),
            },
        };
        var picture = new PictureAdjustmentViewModel(repository, new InertTarget());
        var menu = new PlayerSettingsMenuViewModel(picture);

        Assert.True(picture.IsNeutral);

        menu.ToggleCommand.Execute(null);

        Assert.Equal(new PictureAdjustment(0.2, 1.3, 1.7), picture.Adjustment);
        Assert.Equal(1, repository.Reads);

        // And only once: the row does not change underneath the gear, so a read on every opening
        // would be a query per click for an answer nobody changed.
        menu.ToggleCommand.Execute(null);
        menu.ToggleCommand.Execute(null);
        Assert.Equal(1, repository.Reads);
    }

    /// <summary>
    /// The same rule read off the built tree instead of off the model, because «the second level
    /// replaces the first» is a claim about what is drawn. Measured: with the bindings pointed at
    /// <c>IsOpen</c> instead of the two level flags, both lists stand on screen at once and every one
    /// of the 1354 interface tests and the whole walk stay green.
    /// </summary>
    [AvaloniaFact]
    public void The_two_levels_are_never_drawn_at_the_same_time()
    {
        var menu = new PlayerSettingsMenuViewModel(new PictureAdjustmentViewModel(
            new SilentPreferences(),
            new InertTarget()));
        var view = new PlayerSettingsMenuView { DataContext = menu };
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var list = view.GetVisualDescendants().OfType<StackPanel>()
            .Single(candidate => candidate.Name == "PlayerSettingsList");
        var group = view.GetVisualDescendants().OfType<StackPanel>()
            .Single(candidate => candidate.Name == "PlayerSettingsPictureGroup");

        Assert.False(list.IsEffectivelyVisible);
        Assert.False(group.IsEffectivelyVisible);

        menu.ToggleCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(list.IsEffectivelyVisible);
        Assert.False(group.IsEffectivelyVisible);

        menu.OpenGroupCommand.Execute(PlayerSettingsGroup.Picture);
        Dispatcher.UIThread.RunJobs();
        Assert.False(list.IsEffectivelyVisible);
        Assert.True(group.IsEffectivelyVisible);

        menu.BackCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(list.IsEffectivelyVisible);
        Assert.False(group.IsEffectivelyVisible);
    }

    /// <summary>
    /// Every group the gear can show fits the band it is drawn in, which is 380 px wide.
    /// </summary>
    /// <remarks>
    /// No other gate knows that number. <c>ViewOverflowTests</c> mounts each view alone at 900 —
    /// the narrowest window the application allows — and a group that fits 900 and not 380 passes
    /// it while being cut off in the one place it is actually drawn. The width is read from the
    /// markup rather than written here, so the two cannot disagree.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(typeof(PictureAdjustmentView))]
    public void Every_group_the_gear_shows_fits_the_band_it_is_drawn_in(Type group)
    {
        var view = (UserControl)Activator.CreateInstance(group)!;
        var window = new Window { Width = GearWidth, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var spilling = view.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Bounds.Width > 0)
            .Where(control => control.Bounds.Right > GearWidth + 0.5)
            .Select(control => $"{control.GetType().Name} reaches {control.Bounds.Right:0.#} px")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            spilling.Length == 0,
            $"{group.Name} does not fit the gear's {GearWidth} px band:\n  "
                + string.Join("\n  ", spilling));

        // Anti-blindness floor: a view that laid out to nothing would spill nowhere and pass.
        var measured = view.GetVisualDescendants().OfType<Control>().Count(c => c.Bounds.Width > 0);
        Assert.True(
            measured >= 10,
            $"only {measured} controls were laid out in {group.Name}, so the tree is not being "
                + "measured rather than the group being small.");
    }

    /// <summary>The band the gear is drawn in, read from the markup that decides it.</summary>
    private static double GearWidth
    {
        get
        {
            var markup = File.ReadAllText(Path.Combine(
                ApSolutions.LocalMedia.TestSupport.RepositoryLayout.Root,
                "src",
                "ApSolutions.LocalMedia.Presentation",
                "Player",
                "PlayerView.axaml"));
            var match = System.Text.RegularExpressions.Regex.Match(
                markup,
                @"x:Name=""PlayerSettingsHostSurface""[^>]*?Width=""(?<width>\d+)""",
                System.Text.RegularExpressions.RegexOptions.Singleline,
                TimeSpan.FromSeconds(2));
            Assert.True(match.Success, "PlayerView.axaml no longer declares the gear's width.");
            return double.Parse(
                match.Groups["width"].Value,
                System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>One stored row, and a count of how many times anything asked for it.</summary>
    private sealed class SilentPreferences : IPlaybackPreferenceRepository
    {
        public PlaybackPreference? Stored { get; init; }

        public int Reads { get; private set; }

        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(Stored);
        }

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>An engine with nothing to adjust, because this scene never looks at the picture.</summary>
    private sealed class InertTarget : IPictureAdjustable
    {
        public PictureAdjustment PictureAdjustment { get; set; } = PictureAdjustment.Neutral;
    }

    /// <summary>A session that answers nothing, because none of this asks it anything.</summary>
    private sealed class SilentCoordinator : IPlaybackSessionCoordinator
    {
        public PlaybackSession? ActiveSession => null;

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlaybackSession(Guid.Empty, request.MediaFileId, request.Path));

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
