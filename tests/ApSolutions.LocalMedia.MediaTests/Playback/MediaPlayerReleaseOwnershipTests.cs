// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Playing hands its media to the one release queue this process owns (BUG-011).
/// </summary>
/// <remarks>
/// The engine used to keep a third queue of its own, and that queue disposed the native media inside
/// its lock with no guard: the first release that threw would leave the worker flag raised for good,
/// so every media opened afterwards leaked in silence. The factory's drain already survives that.
/// What is observable from outside is where the media rests, which is why this asserts on the
/// factory's count rather than on the engine's internals.
/// </remarks>
[Trait("Category", "RealMedia")]
public sealed class MediaPlayerReleaseOwnershipTests
{
    private const string SampleRecipe =
        "-f lavfi -i testsrc2=size=320x240:rate=15:duration=2 " +
        "-f lavfi -i sine=frequency=440:duration=2 " +
        "-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -t 2";

    [Fact]
    public async Task A_detached_media_rests_in_the_factorys_queue()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            "BUG011/engine-sample.mp4",
            SampleRecipe,
            TestContext.Current.CancellationToken);

        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(
                new MediaFileId(Guid.NewGuid()),
                path,
                useHardwareAcceleration: false),
            TestContext.Current.CancellationToken);
        // The total and not the pending count, because the pending count is a level the drain lowers
        // a second after each media reaches it. Asserting on it raced the drain and lost on a hosted
        // runner four times slower than this machine, where the stop itself outlasts the quiescence
        // window: red on CI on 2026-08-24, green here, with nothing between the two but speed. What
        // the contract says is that the media passed through the queue, and a total says that.
        var handedOverBefore = LibVlcFactory.DeferredReleaseTotal;

        await engine.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(
            LibVlcFactory.DeferredReleaseTotal > handedOverBefore,
            "Stopping detached the media without handing it to the factory's deferred release, so it "
                + "is being freed by something else — and whatever that is, its failure is not the one "
                + "the drain was hardened against.");
    }
}
