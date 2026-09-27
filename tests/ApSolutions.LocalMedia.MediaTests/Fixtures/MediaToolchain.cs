// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.MediaTests.Fixtures;

/// <summary>
/// Locates the local encoder and materialises small, synthetic, license-free media samples
/// under the ignored <c>artifacts/test-media</c> tree. No user media is ever read or copied.
/// </summary>
internal static class MediaToolchain
{
    private static readonly SemaphoreSlim GenerationLock = new(1, 1);
    private static readonly Lazy<HashSet<string>> AvailableEncoders = new(ReadEncoders);

    public static string RepositoryRoot { get; } = RepositoryLayout.Root;

    public static string OutputRoot { get; } = Path.Combine(RepositoryRoot, "artifacts", "test-media");

    public static string? EncoderPath { get; } = FindEncoder();

    public static string MissingEncoderReason =>
        "ffmpeg was not found. Set FFMPEG_PATH or install ffmpeg to generate the synthetic media matrix.";

    /// <summary>True when the local encoder can produce the named stream; never guessed.</summary>
    public static bool HasEncoder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return EncoderPath is not null && AvailableEncoders.Value.Contains(name);
    }

    /// <summary>Writes a small text file the encoder muxes in, such as an internal subtitle track.</summary>
    public static async Task<string> EnsureTextCompanionAsync(
        string relativePath,
        string content,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        var destination = Path.Combine(OutputRoot, relativePath);

        await GenerationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (!File.Exists(destination))
            {
                await File.WriteAllTextAsync(destination, content, cancellationToken).ConfigureAwait(false);
            }

            return destination;
        }
        finally
        {
            GenerationLock.Release();
        }
    }

    /// <summary>Copies the leading fraction of a sample so the trailing index never arrives.</summary>
    public static async Task<string> EnsureTruncatedSampleAsync(
        string relativePath,
        string sourcePath,
        double fraction,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(fraction, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fraction, 1);
        var destination = Path.Combine(OutputRoot, relativePath);

        await GenerationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(destination) && new FileInfo(destination).Length > 0)
            {
                return destination;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var length = Math.Max(1, (int)(bytes.Length * fraction));
            await File.WriteAllBytesAsync(
                destination,
                bytes.AsMemory(0, length),
                cancellationToken).ConfigureAwait(false);
            return destination;
        }
        finally
        {
            GenerationLock.Release();
        }
    }

    public static async Task<string> EnsureSampleAsync(
        string relativePath,
        string arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);
        var encoder = EncoderPath ?? throw new InvalidOperationException(MissingEncoderReason);
        var destination = Path.Combine(OutputRoot, relativePath);

        await GenerationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(destination) && new FileInfo(destination).Length > 0)
            {
                return destination;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            var limit = timeout ?? DefaultGenerationTimeout;
            try
            {
                await RunAsync(
                        encoder,
                        $"-hide_banner -loglevel error -nostdin {arguments} \"{destination}\"",
                        limit,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException exception)
            {
                DeletePartial(destination);
                throw new TimeoutException(
                    $"The encoder was stopped after {limit} producing '{relativePath}', so the recipe "
                    + $"never ends — a duration placed where it bounds only one input is the usual cause. Recipe: {arguments}",
                    exception);
            }
            catch
            {
                DeletePartial(destination);
                throw;
            }

            if (!File.Exists(destination))
            {
                throw new InvalidOperationException($"The encoder did not produce '{relativePath}'.");
            }

            return destination;
        }
        finally
        {
            GenerationLock.Release();
        }
    }

    /// <summary>
    /// Generous for samples of a few seconds, and short enough that a recipe with no end is stopped
    /// long before it fills a disk: one wrote 13.9 GB into the working tree in 47 minutes.
    /// </summary>
    public static TimeSpan DefaultGenerationTimeout { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A sample that exists with a non-zero length is reused as it is, so a half-written one left
    /// behind would be read by every later run as a real sample.
    /// </summary>
    private static void DeletePartial(string destination)
    {
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }
    }

    /// <summary>
    /// Runs the encoder and waits for it, killing it — with anything it started — when the time is up
    /// or the caller cancels.
    /// </summary>
    /// <remarks>
    /// The streams are read without the token on purpose: a read from a process pipe does not answer
    /// a cancellation on Windows, so the earlier version, which read with the token, went on waiting
    /// while the encoder went on writing. The wait is what gets cancelled, and the kill is what ends
    /// the reads.
    /// </remarks>
    private static async Task RunAsync(
        string fileName,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        _ = process.Start();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var errorRead = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(output, errorRead).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException();
        }

        _ = await output.ConfigureAwait(false);
        var error = await errorRead.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Encoder failed with exit code {process.ExitCode}: {error}");
        }
    }

    /// <summary>Asks the local encoder which streams it can write; an absent tool yields nothing.</summary>
    private static HashSet<string> ReadEncoders()
    {
        if (EncoderPath is not { } encoder)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(encoder, "-hide_banner -loglevel error -encoders")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        _ = process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Rows look like " V....D libx264   H.264"; the name is the second whitespace-separated field.
            var fields = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length >= 2 && fields[0].Length == 6)
            {
                _ = names.Add(fields[1]);
            }
        }

        return names;
    }

    private static string? FindEncoder()
    {
        var configured = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        var candidates = new List<string> { @"C:\ffmpeg\bin\ffmpeg.exe" };
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        candidates.AddRange(pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg")));

        return candidates.FirstOrDefault(File.Exists);
    }
}
