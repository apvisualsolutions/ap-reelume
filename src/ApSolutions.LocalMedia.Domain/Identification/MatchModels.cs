// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Identification;

public enum ParsedMediaKind
{
    Unknown,
    Movie,
    Episode,
}

public sealed record FileNameContext(
    string FileName,
    IReadOnlyList<string> ParentFolders);

public sealed record ParsedMediaName(
    ParsedMediaKind Kind,
    string CleanTitle,
    int? Year,
    int? Season,
    int? Episode,
    int? AbsoluteEpisode,
    bool IsSpecial,
    IReadOnlyList<string> NoiseTags,
    IReadOnlyList<string> ParseWarnings)
{
    public string OriginalName { get; init; } = string.Empty;
}

public interface IMediaNameParser
{
    ParsedMediaName Parse(FileNameContext context);
}

public readonly record struct CandidateId(Guid Value)
{
    public static CandidateId FromStableKey(string stableKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stableKey);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(stableKey));
        return new CandidateId(new Guid(hash.AsSpan(0, 16)));
    }
}

public enum CandidateContentKind
{
    Movie,
    Episode,
}

public enum ReviewState
{
    Pending,
    Suggested,
    Automatic,
    Accepted,
    Rejected,
}

public sealed record CandidateFacts(
    CandidateId CandidateId,
    string StableKey,
    CandidateContentKind Kind,
    double TitleSimilarity,
    double? SeasonMatch,
    double? EpisodeMatch,
    double? YearMatch,
    double? DurationMatch);

public sealed record MatchSignal(
    string Code,
    double Value,
    double Weight);

public sealed record MatchCandidate(
    CandidateId Id,
    MediaFileId MediaFileId,
    string StableKey,
    CandidateContentKind Kind,
    double Score,
    int ScoringModelVersion,
    ReviewState ReviewState,
    IReadOnlyList<MatchSignal> Signals,
    IReadOnlyList<string> ExplanationCodes,
    int Revision = 0,
    bool IsDecisionLocked = false);

public enum MatchDecisionWriteOutcome
{
    Applied,
    Conflict,
    NotFound,
}

public sealed record MatchDecisionWriteResult(
    MatchDecisionWriteOutcome Outcome,
    MatchCandidate? Candidate);
