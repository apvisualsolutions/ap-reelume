// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Media;

/// <summary>
/// Cataloguing and playing share one native instance and one release queue (BUG-010).
/// </summary>
/// <remarks>
/// The probe used to keep both of its own, which the counter could not see. What is observable from
/// outside is the queue: a media the probe finished with has to be resting in the factory's deferred
/// release, because that is the queue whose drain survives a native dispose that throws.
/// </remarks>
[Trait("Category", "RealMedia")]
public sealed class MediaProbeInstanceTests
{
    private const string SampleRecipe =
        "-f lavfi -i testsrc2=size=320x240:rate=15:duration=2 " +
        "-f lavfi -i sine=frequency=440:duration=2 " +
        "-c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -b:a 64k -t 2";

    [Fact]
    public async Task A_probed_media_rests_in_the_factorys_queue_and_adds_no_native_instance()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            "BUG010/probe-sample.mp4",
            SampleRecipe,
            TestContext.Current.CancellationToken);

        await using var factory = LibVlcFactory.CreateHeadless();
        var instancesBefore = LibVlcFactory.NativeInstanceCount;
        var probe = new LibVlcMediaProbe(factory);

        var metadata = await probe.ProbeAsync(path, TestContext.Current.CancellationToken);

        Assert.NotNull(metadata.Duration);
        Assert.True(
            LibVlcFactory.PendingDeferredReleaseCount > 0,
            "The probed media was not handed to the factory's deferred release, so it is being freed "
                + "by something else — and whatever that is, its failure is not the one the drain "
                + "was hardened against.");
        Assert.Equal(instancesBefore, LibVlcFactory.NativeInstanceCount);
    }
}
