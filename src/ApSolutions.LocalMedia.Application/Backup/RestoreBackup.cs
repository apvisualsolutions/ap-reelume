using ApSolutions.LocalMedia.Domain.Backup;

namespace ApSolutions.LocalMedia.Application.Backup;

/// <summary>
/// Puts a restored library in place of the live one.
/// <para>
/// The order is the whole point. Validate, unpack, open, remap — all of it on a copy nobody is using —
/// and only then swap, keeping the database that was replaced. Anything that goes wrong at any of those
/// steps leaves the live database exactly as it was, and says why rather than throwing at a person who
/// is already having a bad day.
/// </para>
/// </summary>
public sealed class RestoreBackup(PreviewRestore preview, IStagedRestoreService staging)
{
    private readonly PreviewRestore _preview = preview ?? throw new ArgumentNullException(nameof(preview));
    private readonly IStagedRestoreService _staging = staging ?? throw new ArgumentNullException(nameof(staging));

    public async Task<RestoreResult> ExecuteAsync(
        string archivePath,
        IReadOnlyList<RootRemap> remaps,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(remaps);

        RestorePreview dryRun;
        string? staged = null;
        try
        {
            (dryRun, staged) = await _preview
                .BuildAsync(archivePath, remaps, keepStaging: true, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            return new RestoreResult(
                false,
                Empty([new RestoreFinding(RestoreFindingKind.UnreadableArchive, exception.Message)]),
                null,
                exception.Message);
        }

        if (!dryRun.CanRestore || staged is null)
        {
            return new RestoreResult(false, dryRun, null, Describe(dryRun));
        }

        try
        {
            progress?.Report(new BackupProgress(BackupStage.Snapshot, 1, 3));
            await _staging.ApplyRemapAsync(staged, dryRun.Roots, cancellationToken).ConfigureAwait(false);
            progress?.Report(new BackupProgress(BackupStage.Manifest, 2, 3));
            var preserved = await _staging.SwapAsync(staged, cancellationToken).ConfigureAwait(false);
            progress?.Report(new BackupProgress(BackupStage.Publish, 3, 3));
            return new RestoreResult(true, dryRun, preserved, null);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or OperationCanceledException)
        {
            _staging.Discard(staged);
            return new RestoreResult(false, dryRun, null, exception.Message);
        }
    }

    private static RestorePreview Empty(IReadOnlyList<RestoreFinding> findings) =>
        new(null, findings, [], 0, 0, 0, 0);

    private static string Describe(RestorePreview preview) =>
        preview.Findings.Count == 0
            ? "The archive could not be restored."
            : string.Join("; ", preview.Findings.Select(finding => $"{finding.Kind}: {finding.Detail}"));
}
