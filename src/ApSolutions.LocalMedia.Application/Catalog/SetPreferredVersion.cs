using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Catalog;

public sealed record SetPreferredVersionCommand(
    MediaVersionId GroupId,
    MediaFileId MediaFileId);

public sealed class SetPreferredVersion
{
    private readonly IMediaVersionGroupRepository _repository;

    public SetPreferredVersion(IMediaVersionGroupRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<MediaVersionGroup> ExecuteAsync(
        SetPreferredVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var group = await _repository.FindByIdAsync(command.GroupId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The media-version group no longer exists.");
        if (!group.Versions.Any(version => version.MediaFileId == command.MediaFileId))
        {
            throw new ArgumentException("The preferred file must belong to the group.", nameof(command));
        }

        var updated = group with { PreferredMediaFileId = command.MediaFileId };
        await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }
}
