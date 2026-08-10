// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Media;

/// <summary>
/// The generator that materialises the codec matrix has to fail out loud when its encoder wedges.
/// </summary>
/// <remarks>
/// Six of the ten CI runs of 2026-08-10 were cancelled at the sixty-minute ceiling, and the log said
/// nothing at all between "Build succeeded" and the cancellation fifty-six minutes later. The step
/// after the build is this generator, and it started FFmpeg with no bound: one wedged encode burned
/// the whole job and reported as "cancelled", which reads like an infrastructure hiccup rather than
/// like a gate that hung.
/// <para>
/// The whole matrix takes 1,6 seconds to produce on a development machine — sixteen samples at about
/// a tenth of a second each — so a per-sample ceiling is not a performance budget in disguise. It is
/// the difference between a named failure and an hour of silence.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Collection(ChildProcessSuites.Name)]
public sealed class TestMediaGeneratorTests
{
    /// <summary>Long enough that a wedged child is the only way to reach it.</summary>
    private static readonly TimeSpan NeverReturned = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task An_encoder_that_never_returns_is_killed_and_named_instead_of_hanging_the_build()
    {
        var workspace = Directory.CreateTempSubdirectory("apsolutions-generator-");
        try
        {
            // Stands in for an FFmpeg that accepted the work and never came back.
            var wedged = Path.Combine(workspace.FullName, "wedged-encoder.cmd");
            await File.WriteAllTextAsync(
                wedged,
                "@echo off\r\nping -n 600 127.0.0.1 > nul\r\n",
                TestContext.Current.CancellationToken);

            var started = Stopwatch.StartNew();
            var run = await RunGeneratorAsync(wedged, Path.Combine(workspace.FullName, "out"));
            started.Stop();

            Assert.True(
                run.Exited,
                $"The generator was still running after {NeverReturned.TotalSeconds:F0} s, which is the "
                    + "failure this exists to prevent: on CI that silence lasts until the job ceiling.");
            Assert.NotEqual(0, run.ExitCode);
            Assert.Contains("did not finish", run.Output, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    private static async Task<(bool Exited, int ExitCode, string Output)> RunGeneratorAsync(
        string encoderPath,
        string outputRoot)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = DatabaseTestHarness.GetRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "-NoProfile",
            "-File",
            Path.Combine(DatabaseTestHarness.GetRepositoryRoot(), "eng", "generate-test-media.ps1"),
            "-Output",
            outputRoot,
            "-Force",
            "-SampleTimeoutSeconds",
            "5",
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["FFMPEG_PATH"] = encoderPath;

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        using var deadline = new CancellationTokenSource(NeverReturned);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            // The test refuses to hang for the same reason the generator must not: killing the tree
            // takes the stand-in encoder with it.
            process.Kill(entireProcessTree: true);
            return (false, 0, string.Empty);
        }

        return (true, process.ExitCode, await output + await error);
    }
}
