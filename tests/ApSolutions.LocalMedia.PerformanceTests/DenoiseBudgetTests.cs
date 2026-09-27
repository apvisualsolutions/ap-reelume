// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

/// <summary>
/// The noise reducer runs on the thread that hands LibVLC's frames over, once per frame, so its cost
/// comes straight out of the time between two frames.
/// </summary>
/// <remarks>
/// Two budgets and not one, because the reducer does not promise the same thing at every size. The
/// film it exists for is standard definition — the file that prompted it is 720×404 — and there it
/// runs the article's method in full. A 1080p frame at a step of one took 18 ms at the 95th
/// percentile on 28 threads, measured on 2026-09-27, which does not fit a 60 fps film and would not
/// fit anything on four cores; the step exists for that, and the budget is written at the step the
/// player falls back to.
/// </remarks>
public sealed class DenoiseBudgetTests
{
    [Fact]
    public async Task A_standard_definition_frame_is_filtered_in_full_inside_five_milliseconds()
    {
        // The same exemption as the frame budget and for the same reason: a shared runner is not
        // the hardware the budget is approved on. Measured here at 2.2 ms.
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true",
            "Shared-runner timing is not the hardware the denoise budget was approved on.");

        await MeasureAsync("denoise-576p-step-1", width: 720, height: 404, step: 1, budgetMilliseconds: 5);
    }

    [Fact]
    public async Task A_full_high_definition_frame_at_a_step_of_two_is_filtered_inside_ten_milliseconds()
    {
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true",
            "Shared-runner timing is not the hardware the denoise budget was approved on.");

        // Measured here at 6.4 ms.
        await MeasureAsync("denoise-1080p-step-2", width: 1920, height: 1080, step: 2, budgetMilliseconds: 10);
    }

    private static async Task MeasureAsync(string metric, int width, int height, int step, double budgetMilliseconds)
    {
        var frame = new byte[width * 2 * height];
        new Random(7).NextBytes(frame);
        var denoiser = new OverlappedDctDenoiser();

        var samples = UiFrameBudgetProbe.Measure(
            () => denoiser.Denoise(frame, width, height, width * 2, strength: 3f, step),
            repetitions: 40);

        await PerformanceEvidence.WriteAsync(metric, samples, budgetMilliseconds, TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < budgetMilliseconds,
            $"{metric} had a p95 of {samples.P95Milliseconds:F3} ms against {budgetMilliseconds} ms.");
    }
}
