using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using ApSolutions.LocalMedia.MediaTests.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Corpus;

/// <summary>
/// A season of episodes means dozens of decode windows opened and released against the same native
/// instance that playback uses. Releasing a player before its media, or a media without quiescence,
/// is the native failure mode this repository keeps relearning — the process dies without a managed
/// exception. Twenty consecutive windows in one process is the drill that makes the order load-bearing.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class SegmentExtractionEnduranceTests
{
    [Fact]
    public async Task Twenty_decode_windows_in_sequence_leave_no_native_resource_behind()
    {
        var sample = MediaManifest.Require("mp4-h264-aac");
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        var extractor = new LocalSegmentFeatureExtractor(factory);
        var instancesBefore = LibVlcFactory.NativeInstanceCount;

        // The extractor caches by path, size, and write time, so one file only ever decodes once.
        // Ten distinct copies defeat the cache: each is an episode of its own, and each extraction
        // decodes an opening and a closing window — twenty full create-decode-release cycles over
        // the shared native instance.
        var arena = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(),
            "ApSolutions.LocalMedia.Tests",
            FormattableString.Invariant($"endurance-{Guid.NewGuid():N}")));
        try
        {
            for (var run = 0; run < 10; run++)
            {
                var copy = Path.Combine(arena.FullName, FormattableString.Invariant($"episode-{run:00}.mp4"));
                File.Copy(path, copy);
                var episode = new SegmentDetectionEpisode(
                    new MediaFileId(Guid.NewGuid()),
                    copy,
                    TimeSpan.FromSeconds(3));

                var prints = await extractor.ExtractAsync(episode, TestContext.Current.CancellationToken);
                Assert.NotNull(prints);
            }
        }
        finally
        {
            arena.Delete(recursive: true);
        }

        Assert.Equal(0, factory.LiveMediaPlayerCount);
        Assert.Equal(instancesBefore, LibVlcFactory.NativeInstanceCount);

        // The deferral is real and it drains. The queue is shared by the whole process and other
        // suites enqueue concurrently, so the proof is not "empty" — it is "shrinking": either the
        // count reaches zero, or it is observed going down, which only a live worker can cause.
        // A dead drain worker would hold its media forever and the count could only grow.
        var highest = LibVlcFactory.PendingDeferredReleaseCount;
        var drained = highest == 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (!drained && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            var current = LibVlcFactory.PendingDeferredReleaseCount;
            drained = current == 0 || current < highest;
            highest = Math.Max(highest, current);
        }

        Assert.True(drained, "No deferred media was ever disposed: the drain worker is not running.");
    }
}
