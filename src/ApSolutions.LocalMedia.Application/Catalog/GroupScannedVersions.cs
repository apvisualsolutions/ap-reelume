// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Catalog;

public sealed record GroupScannedVersionsResult(int GroupedCount, int HeldForConfirmationCount);

/// <summary>
/// Finds the duplicates a scan surfaced and turns them into version groups. This is the caller the
/// audit found missing (LIB-008): <c>GroupMediaVersions</c> had a repository, a policy, and tests,
/// and nothing in the application ever invoked it, so groups were never created on their own.
/// </summary>
/// <remarks>
/// Two files are the same content when their parsed names say so and the grouping policy agrees —
/// the same rule T15 proved. A set whose durations differ materially is left for a person
/// (<c>ConfirmationRequired</c>), no file is ever deleted or hidden, and a stored preferred version
/// survives every later scan because <c>GroupMediaVersions</c> merges instead of replacing.
/// </remarks>
public sealed class GroupScannedVersions
{
    private readonly ILibraryRootRepository _roots;
    private readonly IMediaFileRepository _mediaFiles;
    private readonly IMediaVersionGroupRepository _groups;
    private readonly IMediaNameParser _parser;
    private readonly DuplicateGroupingPolicy _policy;
    private readonly GroupMediaVersions _group;

    public GroupScannedVersions(
        ILibraryRootRepository roots,
        IMediaFileRepository mediaFiles,
        IMediaVersionGroupRepository groups,
        IMediaNameParser parser,
        DuplicateGroupingPolicy policy,
        GroupMediaVersions group)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _mediaFiles = mediaFiles ?? throw new ArgumentNullException(nameof(mediaFiles));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _group = group ?? throw new ArgumentNullException(nameof(group));
    }

    public async Task<GroupScannedVersionsResult> ExecuteAsync(
        ScanSummary summary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summary);
        if (summary.IsCancelled
            || await _roots.GetAsync(summary.RootId, cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return new GroupScannedVersionsResult(0, 0);
        }

        // Every file the scan saw, keyed by what its name says it is. The whole root's files take
        // part — a copy added today must group with the copy catalogued last month.
        var matches = new List<(DuplicateFileMatch Match, MediaFile File)>();
        foreach (var item in summary.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.Outcome is not (ScanItemOutcome.Added or ScanItemOutcome.Updated or ScanItemOutcome.Unchanged)
                || await _mediaFiles.FindByPathAsync(summary.RootId, item.Path, cancellationToken)
                    .ConfigureAwait(false) is not { } file)
            {
                continue;
            }

            var parsed = _parser.Parse(FileNameContext.ForFile(item.Path, root.Path));
            if (parsed.Kind is not (ParsedMediaKind.Movie or ParsedMediaKind.Episode)
                || string.IsNullOrWhiteSpace(parsed.CleanTitle))
            {
                continue;
            }

            matches.Add((new DuplicateFileMatch(file.Id, StableContentKey(parsed), parsed), file));
        }

        var grouped = 0;
        var held = 0;
        foreach (var set in matches
            .GroupBy(entry => entry.Match.StableContentKey, StringComparer.Ordinal)
            .Where(set => set.Count() > 1))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = _policy.Assess([.. set.Select(entry => entry.Match)]);
            if (!decision.CanGroup)
            {
                held++;
                continue;
            }

            var result = await _group.ExecuteAsync(
                    new GroupMediaVersionsCommand(
                        await ResolveGroupKeyAsync(set.Select(entry => entry.Match.MediaFileId), cancellationToken)
                            .ConfigureAwait(false),
                        [.. set.Select(entry => CreateVersion(entry.File))],
                        ConfirmDifferentEditions: false),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Outcome == MediaVersionGroupingOutcome.Grouped)
            {
                grouped++;
            }
            else
            {
                held++;
            }
        }

        return new GroupScannedVersionsResult(grouped, held);
    }

    /// <summary>
    /// The key the group is stored under: the one an existing group already uses, or the title key
    /// of the set's lowest file identifier. Reusing the existing key keeps a group stable when a
    /// copy with an even lower identifier appears later.
    /// </summary>
    private async Task<string> ResolveGroupKeyAsync(
        IEnumerable<MediaFileId> members,
        CancellationToken cancellationToken)
    {
        var ordered = members
            .OrderBy(member => member.Value.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        foreach (var member in ordered)
        {
            if (await _groups.FindByMemberAsync(member, cancellationToken).ConfigureAwait(false) is { } existing)
            {
                return existing.ContentKey;
            }
        }

        return ContentKey.ForTitle(new TitleId(ordered[0].Value)).Value;
    }

    /// <summary>What one parsed name is, without anything a release tag or a path could vary.</summary>
    private static string StableContentKey(ParsedMediaName parsed)
    {
        var title = parsed.CleanTitle.Trim().ToUpperInvariant();
        return parsed.Kind == ParsedMediaKind.Episode
            ? $"episode:{title}:s{parsed.Season}:e{parsed.Episode}"
            : $"movie:{title}:y{parsed.Year}";
    }

    private static MediaVersion CreateVersion(MediaFile file) => new(
        file.Id,
        file.Path,
        file.IsAvailable,
        file.TechnicalMetadata.Duration,
        file.TechnicalMetadata.Width,
        file.TechnicalMetadata.Height,
        IsHdr: false,
        file.TechnicalMetadata.VideoCodecs.Count > 0 ? file.TechnicalMetadata.VideoCodecs[0] : string.Empty,
        file.SizeBytes);

}
