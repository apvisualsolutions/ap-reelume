// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Catalog;

public sealed record MediaVersion(
    MediaFileId MediaFileId,
    string Path,
    bool IsAvailable,
    TimeSpan? Duration,
    int? Width,
    int? Height,
    bool IsHdr,
    string VideoCodec,
    long SizeBytes);

public sealed record MediaVersionGroup(
    MediaVersionId Id,
    string ContentKey,
    IReadOnlyList<MediaVersion> Versions,
    MediaFileId? PreferredMediaFileId);

public sealed record MediaVersionPreferences(bool PreferHdr);

public sealed record MediaVersionSelection(
    MediaVersion? EffectiveVersion,
    MediaFileId? StoredPreferredMediaFileId,
    IReadOnlyList<MediaFileId> VisibleFileIds);

public interface IMediaVersionGroupRepository
{
    Task<MediaVersionGroup?> FindByContentKeyAsync(
        string contentKey,
        CancellationToken cancellationToken = default);

    Task<MediaVersionGroup?> FindByIdAsync(
        MediaVersionId groupId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The group one file belongs to, whichever key the group was stored under. Unidentified copies
    /// each carry their own title, so a lookup by one title's key cannot see a group created from
    /// another copy's card — this one can.
    /// </summary>
    Task<MediaVersionGroup?> FindByMemberAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(MediaVersionGroup group, CancellationToken cancellationToken = default);
}

public sealed class MediaVersionSelectionPolicy
{
    private readonly Dictionary<string, int> _codecPriority = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AV1"] = 3,
        ["HEVC"] = 2,
        ["H265"] = 2,
        ["H264"] = 1,
    };

    public MediaVersionSelection Select(MediaVersionGroup group, MediaVersionPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(preferences);
        if (group.Versions.Count == 0)
        {
            throw new ArgumentException("A media-version group cannot be empty.", nameof(group));
        }

        var selected = group.Versions
            .OrderByDescending(version => version.IsAvailable)
            .ThenByDescending(version => version.MediaFileId == group.PreferredMediaFileId)
            .ThenByDescending(PixelCount)
            .ThenByDescending(version => preferences.PreferHdr && version.IsHdr)
            .ThenByDescending(version => _codecPriority.GetValueOrDefault(version.VideoCodec))
            .ThenByDescending(version => version.SizeBytes)
            .ThenBy(version => version.MediaFileId.Value.ToString("D"), StringComparer.Ordinal)
            .First();
        return new MediaVersionSelection(
            selected,
            group.PreferredMediaFileId,
            group.Versions.Select(version => version.MediaFileId).ToArray());
    }

    private static long PixelCount(MediaVersion version) =>
        Math.Max(0, version.Width ?? 0) * (long)Math.Max(0, version.Height ?? 0);
}
