// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Identification;

public interface ICandidateScorer
{
    MatchCandidate Score(MediaFileId mediaFileId, ParsedMediaName parsed, CandidateFacts facts);
}

public sealed class CandidateScorer : ICandidateScorer
{
    public const int ScoringModelVersion = 1;
    private const double TitleWeight = 0.50;
    private const double EpisodeWeight = 0.20;
    private const double SeasonWeight = 0.15;
    private const double YearWeight = 0.10;
    private const double DurationWeight = 0.05;

    public MatchCandidate Score(MediaFileId mediaFileId, ParsedMediaName parsed, CandidateFacts facts)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(facts);
        ValidateFacts(facts);

        if (HasKindConflict(parsed.Kind, facts.Kind))
        {
            return new MatchCandidate(
                facts.CandidateId,
                mediaFileId,
                facts.StableKey,
                facts.Kind,
                Score: 0,
                ScoringModelVersion,
                ReviewState.Rejected,
                Signals: [],
                ExplanationCodes: ["Identification.Error.KindConflict"]);
        }

        var signals = BuildSignals(parsed, facts);
        var applicableWeight = signals.Sum(signal => signal.Weight);
        var score = signals.Sum(signal => signal.Value * signal.Weight) / applicableWeight;
        var explanations = signals
            .Where(signal => signal.Value > 0)
            .OrderByDescending(signal => signal.Weight)
            .Select(signal => signal.Code)
            .ToList();

        if (HasEpisodeContradiction(parsed, facts))
        {
            score = Math.Min(score, 0.59);
            explanations.Add("Identification.Warning.EpisodeContradiction");
        }
        else if (parsed.ParseWarnings.Contains("AmbiguousCompactEpisode", StringComparer.Ordinal))
        {
            score = Math.Min(score, 0.89);
            explanations.Add("Identification.Warning.AmbiguousName");
        }

        score = Math.Round(score, 4, MidpointRounding.AwayFromZero);
        return new MatchCandidate(
            facts.CandidateId,
            mediaFileId,
            facts.StableKey,
            facts.Kind,
            score,
            ScoringModelVersion,
            ConfidencePolicy.Classify(score),
            signals.AsReadOnly(),
            explanations.AsReadOnly());
    }

    private static List<MatchSignal> BuildSignals(ParsedMediaName parsed, CandidateFacts facts)
    {
        var signals = new List<MatchSignal>
        {
            new("Identification.Signal.Title", facts.TitleSimilarity, TitleWeight),
        };
        if (parsed.Episode.HasValue && facts.EpisodeMatch.HasValue)
        {
            signals.Add(new MatchSignal("Identification.Signal.Episode", facts.EpisodeMatch.Value, EpisodeWeight));
        }

        if (parsed.Season.HasValue && facts.SeasonMatch.HasValue)
        {
            signals.Add(new MatchSignal("Identification.Signal.Season", facts.SeasonMatch.Value, SeasonWeight));
        }

        if (parsed.Year.HasValue && facts.YearMatch.HasValue)
        {
            signals.Add(new MatchSignal("Identification.Signal.Year", facts.YearMatch.Value, YearWeight));
        }

        if (facts.DurationMatch.HasValue)
        {
            signals.Add(new MatchSignal("Identification.Signal.Duration", facts.DurationMatch.Value, DurationWeight));
        }

        return signals;
    }

    private static bool HasKindConflict(ParsedMediaKind parsedKind, CandidateContentKind candidateKind) =>
        (parsedKind == ParsedMediaKind.Movie && candidateKind != CandidateContentKind.Movie)
        || (parsedKind == ParsedMediaKind.Episode && candidateKind != CandidateContentKind.Episode);

    private static bool HasEpisodeContradiction(ParsedMediaName parsed, CandidateFacts facts) =>
        (parsed.Season.HasValue && facts.SeasonMatch == 0)
        || (parsed.Episode.HasValue && facts.EpisodeMatch == 0);

    private static void ValidateFacts(CandidateFacts facts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(facts.StableKey);
        foreach (var value in new[]
                 {
                     facts.TitleSimilarity,
                     facts.SeasonMatch,
                     facts.EpisodeMatch,
                     facts.YearMatch,
                     facts.DurationMatch,
                 })
        {
            if (value.HasValue && (!double.IsFinite(value.Value) || value.Value is < 0 or > 1))
            {
                throw new ArgumentOutOfRangeException(nameof(facts));
            }
        }
    }
}
