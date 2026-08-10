// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Common;

namespace ApSolutions.LocalMedia.Application.Privacy;

/// <summary>
/// Produces the diagnostic report a person can read before deciding to do anything with it.
/// <para>
/// There is no transport here on purpose. The MVP writes one file, in a folder a person opens, and
/// nothing sends it anywhere: what happens to it afterwards is their decision, not the application's.
/// The preview and the file are produced by the same call, so what is shown is what exists.
/// </para>
/// </summary>
public sealed class CreateDiagnostics(IDiagnosticsBuilder builder, IAppDataPaths paths, IClock clock)
{
    private readonly IDiagnosticsBuilder _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    private readonly IAppDataPaths _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    private readonly IClock _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>The report exactly as it would be written, or null when consent has not been given.</summary>
    public DiagnosticsReport? Preview(DiagnosticsConsent consent, DiagnosticsInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(consent);
        ArgumentNullException.ThrowIfNull(inputs);
        return _builder.Build(consent, inputs);
    }

    /// <summary>
    /// Writes the report and returns where it went, or null when there was nothing to write. Without
    /// consent the diagnostics folder is not even created: an empty folder is still a trace.
    /// </summary>
    public async Task<string?> ExportAsync(
        DiagnosticsConsent consent,
        DiagnosticsInputs inputs,
        CancellationToken cancellationToken = default)
    {
        if (Preview(consent, inputs) is not { } report)
        {
            return null;
        }

        _ = _clock.UtcNow;
        Directory.CreateDirectory(_paths.DiagnosticsDirectory);
        var destination = Path.Combine(_paths.DiagnosticsDirectory, DiagnosticsReport.FileName);
        var temporaryPath = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                DiagnosticsSerialization.Serialize(report),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destination, overwrite: true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// Removes the report a previous export wrote, and the folder with it.
    /// <para>
    /// Taking a permission back has to reach what the permission produced. A report that outlives the
    /// consent that created it is a description of somebody's machine kept for a reason nobody agreed
    /// to any more, and an empty diagnostics folder is still a trace that one existed.
    /// </para>
    /// </summary>
    public Task DiscardAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(_paths.DiagnosticsDirectory))
        {
            Directory.Delete(_paths.DiagnosticsDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }
}
