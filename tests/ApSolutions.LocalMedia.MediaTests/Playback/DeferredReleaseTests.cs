// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The factory's teardown decisions, asked for on purpose instead of happened upon.
/// </summary>
/// <remarks>
/// A flush that exhausts its ceiling was reached only when a runner was slow enough for the shared
/// queue to outlast five seconds: five CI runs of the same tree took that branch once and skipped it
/// four times, which moved this file's coverage with nothing in it changed. What a suite happens to
/// be busy with is not a test, so the ceiling is asked for here — below the quiescence window, where
/// giving up is the only outcome the clock allows — and the giving up is asserted.
/// </remarks>
public sealed class DeferredReleaseTests
{
    [Fact]
    public async Task A_ceiling_below_the_quiescence_window_gives_up_rather_than_waiting()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        var path = Path.Combine(Path.GetTempPath(), $"apreelume-deferred-{Guid.NewGuid():N}.mp4");
        await File.WriteAllBytesAsync(path, [], TestContext.Current.CancellationToken);
        try
        {
            // A media rests for a second before its handle is disposed, so a ceiling of one
            // millisecond cannot see the queue empty however fast the machine is.
            factory.DeferRelease(factory.CreateMedia(path));

            var flushed = await LibVlcFactory.FlushDeferredReleasesAsync(TimeSpan.FromMilliseconds(1));

            Assert.False(
                flushed,
                "The flush reported the queue empty while a media was still resting in it, so a "
                    + "caller would let go of the player its media referenced — which is the native "
                    + "teardown crash this ordering exists to prevent.");
        }
        finally
        {
            _ = await LibVlcFactory.FlushDeferredReleasesAsync(TimeSpan.FromSeconds(30));
            File.Delete(path);
        }
    }

    [Fact]
    public async Task A_ceiling_that_cannot_wait_at_all_is_refused()
    {
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => LibVlcFactory.FlushDeferredReleasesAsync(TimeSpan.Zero));
    }

    [Fact]
    public async Task Disposing_a_second_time_changes_nothing()
    {
        var factory = LibVlcFactory.CreateHeadless();

        await factory.DisposeAsync();
        await factory.DisposeAsync();

        _ = Assert.Throws<ObjectDisposedException>(factory.CreateMediaPlayer);
    }

    [Fact]
    public async Task Disposing_returns_the_players_a_caller_kept()
    {
        var factory = LibVlcFactory.CreateHeadless();
        _ = factory.CreateMediaPlayer();
        Assert.Equal(1, factory.LiveMediaPlayerCount);

        await factory.DisposeAsync();

        Assert.Equal(0, factory.LiveMediaPlayerCount);
    }
}
