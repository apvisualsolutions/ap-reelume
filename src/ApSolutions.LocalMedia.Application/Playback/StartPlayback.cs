using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>Opens one media file in the embedded engine through the single-session coordinator.</summary>
public sealed class StartPlayback
{
    private readonly IPlaybackSessionCoordinator _coordinator;

    public StartPlayback(IPlaybackSessionCoordinator coordinator) =>
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public Task<PlaybackSession> ExecuteAsync(
        PlaybackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _coordinator.StartAsync(request, cancellationToken);
    }
}
