using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>Answers what comes after the episode that is playing, or nothing at all.</summary>
public sealed class GetNextEpisode
{
    private readonly IEpisodeSequenceRepository _repository;

    public GetNextEpisode(IEpisodeSequenceRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<EpisodeSequenceEntry?> ExecuteAsync(
        TitleId showId,
        EpisodeId currentEpisode,
        CancellationToken cancellationToken = default)
    {
        var episodes = await _repository.GetSeriesAsync(showId, cancellationToken).ConfigureAwait(false);
        return NextEpisodePolicy.FindNext(episodes, currentEpisode);
    }
}
