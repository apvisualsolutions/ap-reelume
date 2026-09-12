// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// The picture adjustment measured where a person would see it: on the frames the engine publishes,
/// decoded from a real file by the production engine.
/// </summary>
/// <remarks>
/// <see cref="PictureAdjustmentTests"/> proves the table is right and
/// <see cref="PackedYuvConverterTests"/> proves the conversion reads it. Neither proves the engine
/// ever hands one over, which is the defect this house is named after — and the only way to tell is
/// to decode something and look at what comes out.
/// </remarks>
public sealed class EnginePictureAdjustmentTests
{
    private const string SampleRelativePath = "PLY16/mpeg2-480p-noisy.mkv";

    private const string SampleRecipe =
        "-f lavfi -i testsrc2=size=720x480:rate=25,noise=alls=10:allf=t -t 6 " +
        "-f lavfi -i sine=frequency=440:duration=6 " +
        "-c:v mpeg2video -b:v 900k -pix_fmt yuv420p -c:a mp2 -b:a 128k -shortest";

    [Fact]
    public async Task A_raised_gamma_reaches_the_frames_the_engine_publishes()
    {
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            SampleRelativePath, SampleRecipe, TestContext.Current.CancellationToken);

        var plain = await MeanBrightnessAsync(path, PictureAdjustment.Neutral);
        var lifted = await MeanBrightnessAsync(
            path, new PictureAdjustment(Brightness: 0d, Contrast: 1d, Gamma: 1.6d));

        // The same frame of the same file, so the only difference is the adjustment. A lift that
        // does not clear a tenth would be inside the noise of which frame arrived first.
        Assert.True(
            lifted > plain * 1.1,
            $"the picture only went from a mean of {plain:F1} to {lifted:F1}, which nobody would see.");
    }

    [Fact]
    public async Task The_neutral_adjustment_leaves_the_engine_publishing_what_it_published_before()
    {
        // The control, and the acceptance criterion of PLY-018 measured at the far end: an engine
        // that quietly applied something of its own would pass the test above just as well.
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            SampleRelativePath, SampleRecipe, TestContext.Current.CancellationToken);

        var untouched = await MeanBrightnessAsync(path, adjustment: null);
        var neutral = await MeanBrightnessAsync(path, PictureAdjustment.Neutral);

        Assert.Equal(untouched, neutral, 3);
    }

    [Fact]
    public async Task A_lowered_gamma_darkens_the_frames_instead_of_lifting_them()
    {
        // The control the test above needs, and an audit found it missing: an engine that ignored
        // its argument and applied a gamma of its own passed «raised gamma lifts» perfectly, because
        // 1.6 was the only value anything ever asked for.
        Assert.SkipWhen(MediaToolchain.EncoderPath is null, MediaToolchain.MissingEncoderReason);
        var path = await MediaToolchain.EnsureSampleAsync(
            SampleRelativePath, SampleRecipe, TestContext.Current.CancellationToken);

        var plain = await MeanBrightnessAsync(path, PictureAdjustment.Neutral);
        var darkened = await MeanBrightnessAsync(
            path, new PictureAdjustment(Brightness: 0d, Contrast: 1d, Gamma: 0.6d));

        Assert.True(
            darkened < plain * 0.9,
            $"the picture went from a mean of {plain:F1} to {darkened:F1} with the gamma BELOW one.");
    }

    [Fact]
    public async Task What_was_asked_for_is_what_the_engine_reports_and_what_it_carries()
    {
        // Two holes an audit measured on 2026-09-12, and neither was visible from the pixels.
        // Dropping the assignment left the getter answering Neutral for ever while the picture was
        // adjusted anyway — a control bound to it would spring back on its own. And building the
        // table unconditionally cost a lookup per pixel at the default setting with every test
        // green, because an identity table and no table produce the same bytes.
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);

        Assert.True(engine.PictureAdjustment.IsNeutral);
        Assert.False(engine.CarriesPictureLookup);

        var asked = new PictureAdjustment(Brightness: 0.1d, Contrast: 1.2d, Gamma: 1.6d);
        engine.PictureAdjustment = asked;

        Assert.Equal(asked, engine.PictureAdjustment);
        Assert.True(engine.CarriesPictureLookup);

        engine.PictureAdjustment = PictureAdjustment.Neutral;

        Assert.False(
            engine.CarriesPictureLookup,
            "going back to neutral left a table behind, so the default is paying for a lookup that changes nothing.");
    }

    [Fact]
    public async Task An_absent_adjustment_is_refused_rather_than_stored_as_nothing()
    {
        // Null would sail through the property and then throw from the frame callback instead —
        // on LibVLC's decode thread, where an exception has nowhere to go and the picture simply
        // stops.
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);

        Assert.Throws<ArgumentNullException>(() => ((IPictureAdjustable)engine).PictureAdjustment = null!);
        Assert.Equal(PictureAdjustment.Neutral, ((IPictureAdjustable)engine).PictureAdjustment);
    }

    private static async Task<double> MeanBrightnessAsync(string path, PictureAdjustment? adjustment)
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        if (adjustment is not null)
        {
            Assert.IsAssignableFrom<IPictureAdjustable>(engine);
            ((IPictureAdjustable)engine).PictureAdjustment = adjustment;
        }

        using var collector = new BrightnessCollector();
        engine.FrameRendered += collector.OnFrameRendered;

        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);
        var mean = await collector.CollectAsync();
        await engine.StopAsync(TestContext.Current.CancellationToken);
        return mean;
    }

    private sealed class BrightnessCollector : IDisposable
    {
        private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(20);
        private readonly Lock _sync = new();
        private readonly TaskCompletionSource<double> _lit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnFrameRendered(object? sender, VideoFrameEventArgs args)
        {
            var span = args.Pixels.Span;
            var total = 0L;
            var counted = 0;

            // Every seventeenth pixel, and the odd number is the whole point: at every sixteenth —
            // which this was — the stride lands on the FIRST luma of every packed pair, for ever.
            // Measured on 2026-09-12: a conversion that adjusted only the first luma of each pair
            // returned the identical 149.2 through this collector, and that defect paints vertical
            // bands down the whole screen. An odd stride alternates between the two halves.
            for (var at = 0; at + 3 < span.Length; at += 68)
            {
                total += span[at] + span[at + 1] + span[at + 2];
                counted += 3;
            }

            if (counted == 0)
            {
                return;
            }

            var mean = total / (double)counted;

            // A decoder can publish a black frame before it has decoded anything, and reading it
            // would measure nothing at all while looking exactly like a measurement.
            if (mean < 8d)
            {
                return;
            }

            lock (_sync)
            {
                _ = _lit.TrySetResult(mean);
            }
        }

        public async Task<double> CollectAsync()
        {
            var finished = await Task.WhenAny(_lit.Task, Task.Delay(FirstFrameTimeout));
            Assert.True(finished == _lit.Task, "No frame with anything in it arrived within 20 seconds.");
            return await _lit.Task;
        }

        public void Dispose() => _lit.TrySetCanceled();
    }
}
