// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The next-episode countdown existed, was tested end to end (T28), and appeared nowhere: the use
/// case was never registered, <c>NextEpisodeViewModel.Offer</c> was called from nowhere, and the
/// engine did not even report that the media had ended — the application had no moment to offer
/// anything at (PLY-011).
/// </summary>
public sealed class NextEpisodeWiringTests
{
    [Fact]
    public void The_engine_reports_the_end_of_the_media_as_a_state()
    {
        var engine = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Infrastructure",
            "Playback",
            "LibVlcMediaPlayerEngine.cs"));

        // Without EndReached the state machine stays at Playing forever and no code path can know
        // an episode finished on its own.
        Assert.Contains("EndReached +=", engine, StringComparison.Ordinal);
        Assert.Contains("PlaybackState.Ended", engine, StringComparison.Ordinal);
    }

    [Fact]
    public void The_end_of_an_episode_starts_the_offer()
    {
        var composition = CompositionSource();

        Assert.Contains("PlaybackState.Ended", composition, StringComparison.Ordinal);
        Assert.Contains("OfferNextEpisodeAsync", composition, StringComparison.Ordinal);
        Assert.Contains(
            "GetRequiredService<StartNextEpisodeCountdown>",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_overlay_is_built_with_a_handler_that_reaches_the_countdown()
    {
        var composition = CompositionSource();

        // Resolved bare, the overlay's two buttons only hid the card; built with the handler they
        // cancel the countdown, and "play now" opens the episode without the wait.
        Assert.Contains("new NextEpisodeViewModel(", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GetRequiredService<NextEpisodeViewModel>",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_countdown_ticks_reach_the_overlay_on_the_interface_thread()
    {
        var composition = CompositionSource();

        Assert.Contains("Ticked +=", composition, StringComparison.Ordinal);
        Assert.Contains("overlay.Tick", composition, StringComparison.Ordinal);
    }

    private static string CompositionSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the host keeps it.");
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
