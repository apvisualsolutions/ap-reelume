// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Fixtures;

/// <summary>
/// A sample recipe that never ends is stopped, named, and leaves nothing behind.
/// </summary>
/// <remarks>
/// Written after a recipe put its duration in front of the second input, which bounds that input
/// and leaves the first one infinite: the encoder wrote 13.9 GB into the working tree, which lives on
/// a network share, for 47 minutes before anyone stopped it by hand. Nothing in the fixture would
/// ever have stopped it.
/// <para>
/// The file left behind matters as much as the time: a sample that exists with a non-zero length is
/// reused as it is by every later run, so a half-written one would be read as a real sample.
/// </para>
/// </remarks>
[Trait("Category", "RealMedia")]
public sealed class MediaToolchainTimeoutTests
{
    // No duration anywhere: the source runs for ever, which is exactly the defect being provoked.
    // -re is what keeps the provocation harmless, and it is not optional. A synthetic source is not
    // paced by its frame rate: the first version of this test, without -re, wrote 28 GB in ten
    // minutes at 64x64 and five frames a second, and the fixture it was proving broken never stopped
    // it. Paced at real time, the same endless recipe writes a few kilobytes a second.
    private const string EndlessRecipe =
        "-re -f lavfi -i testsrc2=size=64x64:rate=5 -c:v libx264 -preset ultrafast -f matroska";

    private const string RelativePath = "generation-timeout/endless-recipe.mkv";

    [Fact]
    public async Task An_endless_recipe_is_stopped_named_and_leaves_no_file()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var destination = Path.Combine(MediaToolchain.OutputRoot, RelativePath);
        if (File.Exists(destination)) { File.Delete(destination); }

        // A guard of its own, well above the timeout under test, so that a fixture without the
        // timeout fails this test instead of repeating the incident.
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        guard.CancelAfter(TimeSpan.FromSeconds(30));

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var failure = await Record.ExceptionAsync(() => MediaToolchain.EnsureSampleAsync(
            RelativePath,
            EndlessRecipe,
            guard.Token,
            TimeSpan.FromSeconds(3)));
        clock.Stop();

        var timedOut = Assert.IsType<TimeoutException>(failure);
        Assert.Contains(RelativePath, timedOut.Message, StringComparison.Ordinal);
        Assert.Contains(EndlessRecipe, timedOut.Message, StringComparison.Ordinal);
        Assert.False(guard.IsCancellationRequested, "The guard fired, so the fixture's own timeout never did.");

        // Stopped, and not merely abandoned. A fixture that stops waiting without killing the encoder
        // returns only when the encoder happens to die — measured once at about fifteen seconds, and
        // more often never — so the time it took is the direct evidence of the kill.
        Assert.True(
            clock.Elapsed < TimeSpan.FromSeconds(10),
            $"The fixture took {clock.Elapsed} to give up on a three-second limit, so the encoder was not killed.");
        Assert.False(File.Exists(destination), "The encoder was stopped but its half-written sample stayed, and it would be reused.");

        // And nothing still holds the path: an encoder left alive keeps its handle, and Windows lets a
        // file be deleted while it is open, so the absence above is not enough on its own.
        await File.WriteAllBytesAsync(destination, [1], TestContext.Current.CancellationToken);
        File.Delete(destination);
    }

    [Fact]
    public async Task A_recipe_that_ends_is_not_disturbed_by_the_timeout()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        const string relativePath = "generation-timeout/bounded-recipe.mkv";
        var stale = Path.Combine(MediaToolchain.OutputRoot, relativePath);
        if (File.Exists(stale)) { File.Delete(stale); }

        var path = await MediaToolchain.EnsureSampleAsync(
            relativePath,
            "-f lavfi -i testsrc2=size=64x64:rate=5:duration=1 -c:v libx264 -preset ultrafast -f matroska",
            TestContext.Current.CancellationToken,
            TimeSpan.FromSeconds(60));

        Assert.True(new FileInfo(path).Length > 0, "A bounded recipe produced nothing.");
    }
}
