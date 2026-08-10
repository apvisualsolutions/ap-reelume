// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Catalog;

public enum MediaVersionGroupingOutcome
{
    Grouped,
    ConfirmationRequired,
}

public sealed record GroupMediaVersionsCommand(
    string ContentKey,
    IReadOnlyList<MediaVersion> Versions,
    bool ConfirmDifferentEditions);

public sealed record GroupMediaVersionsResult(
    MediaVersionGroupingOutcome Outcome,
    MediaVersionGroup? Group);

public sealed class GroupMediaVersions
{
    private readonly IMediaVersionGroupRepository _repository;

    public GroupMediaVersions(IMediaVersionGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GroupMediaVersionsResult> ExecuteAsync(
        GroupMediaVersionsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ContentKey);
        ArgumentNullException.ThrowIfNull(command.Versions);
        if (command.Versions.Count < 2)
        {
            throw new ArgumentException("At least two versions are required.", nameof(command));
        }

        var existing = await _repository.FindByContentKeyAsync(
            command.ContentKey,
            cancellationToken).ConfigureAwait(false);
        var versions = (existing?.Versions ?? [])
            .Concat(command.Versions)
            .GroupBy(version => version.MediaFileId)
            .Select(group => group.Last())
            .OrderBy(version => version.MediaFileId.Value.ToString("D"), StringComparer.Ordinal)
            .ToArray();
        if (!command.ConfirmDifferentEditions && HasMaterialDurationDifference(versions))
        {
            return new GroupMediaVersionsResult(MediaVersionGroupingOutcome.ConfirmationRequired, null);
        }

        MediaFileId? preferred = existing?.PreferredMediaFileId is { } preferredId
            && versions.Any(version => version.MediaFileId == preferredId)
                ? preferredId
                : null;
        var group = new MediaVersionGroup(
            existing?.Id ?? CreateStableGroupId(command.ContentKey),
            command.ContentKey,
            versions,
            preferred);
        await _repository.SaveAsync(group, cancellationToken).ConfigureAwait(false);
        return new GroupMediaVersionsResult(MediaVersionGroupingOutcome.Grouped, group);
    }

    private static bool HasMaterialDurationDifference(IReadOnlyList<MediaVersion> versions)
    {
        var durations = versions
            .Select(version => version.Duration)
            .Where(duration => duration.HasValue)
            .Select(duration => duration!.Value)
            .Order()
            .ToArray();
        if (durations.Length < 2)
        {
            return false;
        }

        var shortest = durations[0];
        var longest = durations[^1];
        var tolerance = TimeSpan.FromTicks(Math.Max(
            TimeSpan.FromSeconds(5).Ticks,
            shortest.Ticks / 100));
        return longest - shortest > tolerance;
    }

    private static MediaVersionId CreateStableGroupId(string contentKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"media-version-group:{contentKey}"));
        return new MediaVersionId(new Guid(hash.AsSpan(0, 16)));
    }
}
