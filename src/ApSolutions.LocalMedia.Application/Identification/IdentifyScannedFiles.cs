// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

public sealed record IdentifyScannedFilesResult(
    int AttemptedCount,
    int IdentifiedCount,
    int FailedCount);

/// <summary>
/// Runs identification over what a scan catalogued. This is the caller the audit found missing:
/// <c>IdentifyMediaFile</c> was registered and tested, the scan produced files, and nothing joined
/// the two — so the review inbox stayed empty forever (LIB-006/007).
/// </summary>
/// <remarks>
/// A file that already has candidates is left alone: an accepted or rejected decision must survive
/// every later scan, and replacing candidates from here would erase it. A file whose identification
/// throws is counted and skipped, because a scan of a thousand files must not lose the other nine
/// hundred and ninety-nine to one unreadable name — and identification must never be the reason a
/// scan's caller sees a failure. Without the provider token the chain stays local by construction,
/// so this opens no connection on its own.
/// </remarks>
public sealed class IdentifyScannedFiles
{
    private readonly ILibraryRootRepository _roots;
    private readonly IMediaFileRepository _mediaFiles;
    private readonly IMatchCandidateRepository _candidates;
    private readonly IdentifyMediaFile _identify;
    private readonly ApplyIdentification _applyIdentification;

    public IdentifyScannedFiles(
        ILibraryRootRepository roots,
        IMediaFileRepository mediaFiles,
        IMatchCandidateRepository candidates,
        IdentifyMediaFile identify,
        ApplyIdentification applyIdentification)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _mediaFiles = mediaFiles ?? throw new ArgumentNullException(nameof(mediaFiles));
        _candidates = candidates ?? throw new ArgumentNullException(nameof(candidates));
        _identify = identify ?? throw new ArgumentNullException(nameof(identify));
        _applyIdentification = applyIdentification ?? throw new ArgumentNullException(nameof(applyIdentification));
    }

    public async Task<IdentifyScannedFilesResult> ExecuteAsync(
        ScanSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);

        // A cancelled scan was somebody deciding to stop; starting a metadata pass on its leftovers
        // would be the opposite of what they asked for.
        if (summary.IsCancelled)
        {
            return new IdentifyScannedFilesResult(0, 0, 0);
        }

        var root = await _roots.GetAsync(summary.RootId, cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return new IdentifyScannedFilesResult(0, 0, 0);
        }

        var attempted = 0;
        var identified = 0;
        var failed = 0;
        foreach (var item in summary.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Unchanged files are revisited on purpose: a library scanned before this caller
            // existed has files no identification ever saw, and the next scan is when they heal.
            if (item.Outcome is not (ScanItemOutcome.Added or ScanItemOutcome.Updated or ScanItemOutcome.Unchanged))
            {
                continue;
            }

            var file = await _mediaFiles.FindByPathAsync(summary.RootId, item.Path, cancellationToken)
                .ConfigureAwait(false);
            if (file is null)
            {
                continue;
            }

            var existing = await _candidates.GetForMediaFileAsync(file.Id, cancellationToken)
                .ConfigureAwait(false);
            if (existing.Count > 0)
            {
                continue;
            }

            attempted++;
            try
            {
                var result = await _identify.ExecuteAsync(
                        new IdentifyMediaFileCommand(file.Id, CreateContext(root.Path, item.Path)),
                        cancellationToken)
                    .ConfigureAwait(false);
                await ApplyConfidentMatchAsync(file.Id, result, cancellationToken).ConfigureAwait(false);
                identified++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failed++;
            }
        }

        return new IdentifyScannedFilesResult(attempted, identified, failed);
    }

    /// <summary>
    /// The automatic half of LIB-007, which until now was calculated and thrown away. A candidate
    /// the scorer trusted on its own is applied without asking, because the whole point of the
    /// threshold is that nobody should have to confirm what is not in doubt; anything below it stays
    /// in the inbox for a person. Only one candidate can reach that state, since it takes a score
    /// no second entry can also hold.
    /// </summary>
    private async Task ApplyConfidentMatchAsync(
        MediaFileId mediaFileId,
        IdentifyMediaFileResult result,
        CancellationToken cancellationToken)
    {
        if (result.Candidates.FirstOrDefault(candidate => candidate.ReviewState == ReviewState.Automatic)
            is not { } confident)
        {
            return;
        }

        _ = await _applyIdentification.ExecuteAsync(
            new ApplyIdentificationCommand(
                mediaFileId,
                confident.StableKey,
                ResolveMatch.ToMetadataKind(confident.Kind)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The folders between the root and the file, outermost first, which is the order the parser
    /// reads a series layout in: show, then season, then the file.
    /// </summary>
    private static FileNameContext CreateContext(string rootPath, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var relative = directory is null ? "." : Path.GetRelativePath(rootPath, directory);
        var folders = relative is "." or ""
            ? Array.Empty<string>()
            : relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
        return new FileNameContext(Path.GetFileName(filePath), folders);
    }
}
