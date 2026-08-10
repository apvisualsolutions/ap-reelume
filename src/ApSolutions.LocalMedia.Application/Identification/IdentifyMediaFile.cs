// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;

namespace ApSolutions.LocalMedia.Application.Identification;

public sealed record IdentifyMediaFileCommand(
    MediaFileId MediaFileId,
    FileNameContext FileNameContext);

public sealed record IdentifyMediaFileResult(
    ParsedMediaName ParsedName,
    IReadOnlyList<MatchCandidate> Candidates);

public interface IIdentificationCandidateSource
{
    Task<IReadOnlyList<CandidateFacts>> GetLocalAsync(
        ParsedMediaName parsed,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CandidateFacts>> GetRemoteAsync(
        ParsedMediaName parsed,
        CancellationToken cancellationToken = default);
}

public sealed class IdentifyMediaFile
{
    private readonly IMediaNameParser _parser;
    private readonly ICandidateScorer _scorer;
    private readonly IIdentificationCandidateSource _candidateSource;
    private readonly IMatchCandidateRepository _repository;

    public IdentifyMediaFile(
        IMediaNameParser parser,
        ICandidateScorer scorer,
        IIdentificationCandidateSource candidateSource,
        IMatchCandidateRepository repository)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
        _candidateSource = candidateSource ?? throw new ArgumentNullException(nameof(candidateSource));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IdentifyMediaFileResult> ExecuteAsync(
        IdentifyMediaFileCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var parsed = _parser.Parse(command.FileNameContext);
        var facts = (await _candidateSource.GetLocalAsync(parsed, cancellationToken).ConfigureAwait(false)).ToList();
        var candidates = Score(command.MediaFileId, parsed, facts);
        if (!candidates.Any(candidate => candidate.ReviewState == ReviewState.Automatic))
        {
            facts.AddRange(await _candidateSource.GetRemoteAsync(parsed, cancellationToken).ConfigureAwait(false));
            candidates = Score(command.MediaFileId, parsed, facts);
        }

        await _repository.ReplaceForMediaFileAsync(
            command.MediaFileId,
            candidates,
            cancellationToken).ConfigureAwait(false);
        return new IdentifyMediaFileResult(parsed, candidates);
    }

    private List<MatchCandidate> Score(
        MediaFileId mediaFileId,
        ParsedMediaName parsed,
        IEnumerable<CandidateFacts> facts) => facts
        .DistinctBy(candidate => candidate.StableKey, StringComparer.Ordinal)
        .Select(candidate => _scorer.Score(mediaFileId, parsed, candidate))
        .OrderByDescending(candidate => candidate.Score)
        .ThenBy(candidate => candidate.StableKey, StringComparer.Ordinal)
        .ToList();
}
