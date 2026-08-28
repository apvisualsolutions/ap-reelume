// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Playback;

/// <summary>
/// Where the mini player was left, and what happens when the file says something impossible.
/// </summary>
/// <remarks>
/// The window has had no frame since 2026-08-28, so a stored placement is not a convenience: a size
/// of zero or a NaN would produce a window with no title bar to grab and nothing to see, and the
/// only way out would be editing the file by hand. Which is how the bad value got there.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class MiniPlayerPlacementTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        "mini-placement",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Nothing_is_remembered_until_the_mini_player_has_been_somewhere()
    {
        Assert.Null(Placements().Read());
    }

    [Fact]
    public void Where_it_was_left_survives_the_application_being_closed_and_opened_again()
    {
        var left = new MiniPlayerPlacement(320, 180, 640, 400);

        Placements().Save(left);

        // A reader of its own, so the answer comes off the disk rather than out of the instance that
        // wrote it — which is the only half of this that a next launch actually exercises.
        Assert.Equal(left, Placements().Read());
    }

    [Theory]
    [InlineData(0, 0, 0, 270)]
    [InlineData(0, 0, 480, 0)]
    [InlineData(0, 0, -480, 270)]
    [InlineData(double.NaN, 0, 480, 270)]
    [InlineData(0, double.PositiveInfinity, 480, 270)]
    [InlineData(0, 0, double.NaN, 270)]
    [InlineData(0, 0, 480, double.PositiveInfinity)]
    public void A_placement_no_window_could_use_is_answered_as_none(
        double x,
        double y,
        double width,
        double height)
    {
        var placements = Placements();
        placements.Save(new MiniPlayerPlacement(120, 120, 480, 270));

        placements.Save(new MiniPlayerPlacement(x, y, width, height));

        // Refused on the way in as well as on the way out: what is read back is the good one, which
        // is only true if the bad one was never written over it.
        Assert.Equal(new MiniPlayerPlacement(120, 120, 480, 270), Placements().Read());
    }

    [Fact]
    public void A_file_edited_by_hand_into_an_unusable_placement_is_answered_as_none()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        File.WriteAllText(
            path,
            """{"player.miniPlacement":{"X":10,"Y":10,"Width":0,"Height":0}}""");

        Assert.Null(new StoredMiniPlayerPlacement(new JsonSettingsStore(path)).Read());
    }

    [Fact]
    public void The_placement_needs_somewhere_to_be_written_and_something_to_write()
    {
        Assert.Throws<ArgumentNullException>(() => new StoredMiniPlayerPlacement(null!));
        Assert.Throws<ArgumentNullException>(() => Placements().Save(null!));
    }

    private StoredMiniPlayerPlacement Placements()
    {
        Directory.CreateDirectory(_directory);
        return new StoredMiniPlayerPlacement(
            new JsonSettingsStore(Path.Combine(_directory, "settings.json")));
    }
}
