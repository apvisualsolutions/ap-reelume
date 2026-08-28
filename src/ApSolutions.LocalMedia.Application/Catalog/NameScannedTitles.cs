// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Catalog;

/// <summary>How many scanned cards a scan renamed, and how many of them gained a year.</summary>
public sealed record NameScannedTitlesResult(int RenamedCount, int DatedCount);

/// <summary>
/// Gives every unidentified file the name its own file name actually says.
/// </summary>
/// <remarks>
/// <para>
/// The scanned projection wrote the file name verbatim — «El Faro de Piedra 2019», and
/// «Neon.Sobre.el.Rio.2022.2160p» before that — with the release year column empty beside it. The
/// parser that takes those apart has existed since the first week and three use cases already call
/// it: identification matches its output against a provider, version grouping compares copies with
/// it, and since 2026-08-25 the series grouping decides episodes with it. What nobody did was use it
/// for the name on the card.
/// </para>
/// <para>
/// A use case of its own rather than a fourth job inside <see cref="GroupScannedEpisodes"/>, whose
/// name would then describe half of it. It is the same shape as its two siblings and runs in the same
/// chain: after every scan, whatever triggered it, decide from the names and write through a port.
/// </para>
/// <para>
/// It walks <b>every</b> file the scan saw, <c>Unchanged</c> included, and that is what makes an
/// already-catalogued library rename itself without re-probing anything: a file whose size and date
/// have not moved is never re-stored, so a projection written once would otherwise keep its old name
/// for as long as the file sat still. Writing the same name twice costs one UPDATE that changes no
/// bytes.
/// </para>
/// <para>
/// Two kinds of file are left alone. An episode is skipped because the series grouping owns what it
/// is called and the grid does not draw its scanned row at all — the projection's own query hides a
/// file that an <c>episode_media</c> link claims. And a name the parser can make nothing of keeps
/// the file name, which is what <see cref="ScannedTitlePolicy"/> decides rather than this.
/// </para>
/// </remarks>
public sealed class NameScannedTitles
{
    private readonly ILibraryRootRepository _roots;
    private readonly IMediaFileRepository _mediaFiles;
    private readonly IMediaNameParser _parser;

    public NameScannedTitles(
        ILibraryRootRepository roots,
        IMediaFileRepository mediaFiles,
        IMediaNameParser parser)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _mediaFiles = mediaFiles ?? throw new ArgumentNullException(nameof(mediaFiles));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public async Task<NameScannedTitlesResult> ExecuteAsync(
        ScanSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.IsCancelled
            || await _roots.GetAsync(summary.RootId, cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return new NameScannedTitlesResult(0, 0);
        }

        var renamed = 0;
        var dated = 0;
        foreach (var item in summary.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Outcome is not (ScanItemOutcome.Added or ScanItemOutcome.Updated or ScanItemOutcome.Unchanged)
                || await _mediaFiles.FindByPathAsync(summary.RootId, item.Path, cancellationToken)
                    .ConfigureAwait(false) is not { } file)
            {
                continue;
            }

            var context = FileNameContext.ForFile(item.Path, root.Path);
            var parsed = _parser.Parse(context);
            if (parsed.Kind == ParsedMediaKind.Episode)
            {
                continue;
            }

            var title = ScannedTitlePolicy.For(context.FileName, parsed);
            await _mediaFiles.SetScannedTitleAsync(file.Id, title, cancellationToken).ConfigureAwait(false);
            renamed++;
            dated += title.Year is null ? 0 : 1;
        }

        return new NameScannedTitlesResult(renamed, dated);
    }
}
