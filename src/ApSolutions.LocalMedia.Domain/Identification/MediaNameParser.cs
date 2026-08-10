// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.Domain.Identification;

public sealed class MediaNameParser : IMediaNameParser
{
    private static readonly Regex StandardEpisodePattern = CreatePattern(
        @"(?:^|[^\p{L}\p{N}])s(?<season>\d{1,3})e(?<episode>\d{1,4})(?:$|[^\p{L}\p{N}])");

    private static readonly Regex CrossEpisodePattern = CreatePattern(
        @"(?:^|[^\p{L}\p{N}])(?<season>\d{1,3})x(?<episode>\d{1,4})(?:$|[^\p{L}\p{N}])");

    private static readonly Regex WrittenEpisodePattern = CreatePattern(
        @"(?:^|[^\p{L}\p{N}])(?:season|temporada)\s*(?<season>\d{1,3})\s*(?:episode|episodio|capitulo|capítulo)\s*(?<episode>\d{1,4})(?:$|[^\p{L}\p{N}])");

    private static readonly Regex CompactEpisodePattern = CreatePattern(
        @"(?:^|[^\p{L}\p{N}])cap(?:itulo|ítulo)?\s*(?<compact>\d{3,4})(?:$|[^\p{L}\p{N}])");

    private static readonly Regex FolderSeasonPattern = CreatePattern(
        @"(?:^|[^\p{L}\p{N}])(?:season|temporada)\s*(?<season>\d{1,3})(?:$|[^\p{L}\p{N}])");

    private static readonly Regex YearPattern = CreatePattern(
        @"(?:^|[^\p{L}\p{N}])(?<year>\d{4})(?:$|[^\p{L}\p{N}])");

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts", ".m2ts",
    };

    private static readonly HashSet<string> KnownNoiseTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "480p", "720p", "1080p", "2160p", "4k", "hdtv", "web-dl", "webrip",
        "bluray", "brrip", "remux", "multi", "hevc", "h264", "h265", "x264",
        "x265", "av1", "aac", "ac3", "eac3", "dts",
    };

    public ParsedMediaName Parse(FileNameContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var originalName = context.FileName;
        var working = StripMediaExtension(originalName);
        var noiseTags = new List<string>();
        working = RemoveBracketedNoise(working, noiseTags);
        working = NormalizeSeparators(working);

        var warnings = new List<string>();
        var kind = ParsedMediaKind.Unknown;
        int? season = null;
        int? episode = null;
        int? year = null;
        var isSpecial = false;

        var episodeMatch = FirstSuccessfulMatch(
            StandardEpisodePattern.Match(working),
            CrossEpisodePattern.Match(working),
            WrittenEpisodePattern.Match(working));

        if (episodeMatch is not null)
        {
            var parsedSeason = ParseNumber(episodeMatch, "season");
            var parsedEpisode = ParseNumber(episodeMatch, "episode");
            if (IsValidSeason(parsedSeason) && IsValidEpisode(parsedEpisode))
            {
                season = parsedSeason;
                episode = parsedEpisode;
                isSpecial = parsedSeason == 0;
                kind = ParsedMediaKind.Episode;
            }
            else
            {
                warnings.Add("NumberOutOfRange");
            }

            working = BlankMatch(working, episodeMatch);
        }
        else
        {
            var compactMatch = CompactEpisodePattern.Match(working);
            if (compactMatch.Success)
            {
                var digits = compactMatch.Groups["compact"].Value;
                var compactSeason = int.Parse(digits[..^2], System.Globalization.CultureInfo.InvariantCulture);
                var compactEpisode = int.Parse(digits[^2..], System.Globalization.CultureInfo.InvariantCulture);
                var contextSeason = FindContextSeason(context.ParentFolders);
                if (compactEpisode == 0)
                {
                    season = contextSeason == compactSeason ? compactSeason : contextSeason;
                    isSpecial = true;
                    warnings.Add("AmbiguousCompactEpisode");
                }
                else if (contextSeason == compactSeason)
                {
                    season = compactSeason;
                    episode = compactEpisode;
                    kind = ParsedMediaKind.Episode;
                }
                else
                {
                    warnings.Add("AmbiguousCompactEpisode");
                }

                working = BlankMatch(working, compactMatch);
            }
        }

        var yearMatch = YearPattern.Match(working);
        if (yearMatch.Success)
        {
            var parsedYear = ParseNumber(yearMatch, "year");
            if (parsedYear is >= 1888 and <= 2100)
            {
                year = parsedYear;
                if (kind == ParsedMediaKind.Unknown && warnings.Count == 0)
                {
                    kind = ParsedMediaKind.Movie;
                }
            }

            working = BlankMatch(working, yearMatch);
        }

        var cleanTitle = RemoveNoiseTokens(working, noiseTags);
        if (kind == ParsedMediaKind.Unknown && warnings.Count == 0)
        {
            warnings.Add("UnrecognizedName");
        }

        return new ParsedMediaName(
            kind,
            cleanTitle,
            year,
            season,
            episode,
            AbsoluteEpisode: null,
            isSpecial,
            noiseTags.AsReadOnly(),
            warnings.AsReadOnly())
        {
            OriginalName = originalName,
        };
    }

    private static Regex CreatePattern(string pattern)
    {
        return new Regex(
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100));
    }

    private static Match? FirstSuccessfulMatch(params Match[] matches)
    {
        return matches.FirstOrDefault(match => match.Success);
    }

    private static int ParseNumber(Match match, string groupName)
    {
        return int.Parse(match.Groups[groupName].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsValidSeason(int value) => value is >= 0 and <= 99;

    private static bool IsValidEpisode(int value) => value is >= 1 and <= 999;

    private static int? FindContextSeason(IReadOnlyList<string> parentFolders)
    {
        foreach (var folder in parentFolders)
        {
            var normalized = NormalizeSeparators(folder);
            var match = FolderSeasonPattern.Match(normalized);
            if (match.Success)
            {
                var season = ParseNumber(match, "season");
                return IsValidSeason(season) ? season : null;
            }
        }

        return null;
    }

    private static string StripMediaExtension(string value)
    {
        var separator = Math.Max(value.LastIndexOf('/'), value.LastIndexOf('\\'));
        var dot = value.LastIndexOf('.');
        if (dot > separator && MediaExtensions.Contains(value[dot..]))
        {
            return value[..dot];
        }

        return value;
    }

    private static string RemoveBracketedNoise(string value, List<string> noiseTags)
    {
        var builder = new StringBuilder(value);
        var index = 0;
        while (index < value.Length)
        {
            var open = value.IndexOf('[', index);
            if (open < 0)
            {
                break;
            }

            var close = value.IndexOf(']', open + 1);
            if (close < 0)
            {
                break;
            }

            var tag = value[(open + 1)..close].Trim();
            if (KnownNoiseTags.Contains(tag))
            {
                noiseTags.Add(tag);
                for (var character = open; character <= close; character++)
                {
                    builder[character] = ' ';
                }
            }

            index = close + 1;
        }

        return builder.ToString();
    }

    private static string NormalizeSeparators(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is '.' or '_' ? ' ' : character);
        }

        return builder.ToString();
    }

    private static string BlankMatch(string value, Match match)
    {
        return string.Concat(value.AsSpan(0, match.Index), " ", value.AsSpan(match.Index + match.Length));
    }

    private static string RemoveNoiseTokens(string value, List<string> noiseTags)
    {
        var cleanTokens = new List<string>();
        foreach (var token in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var comparisonToken = token.Trim('(', ')', '[', ']', '{', '}', '-', '.');
            if (KnownNoiseTags.Contains(comparisonToken))
            {
                noiseTags.Add(comparisonToken);
                continue;
            }

            if (token is "(" or ")")
            {
                continue;
            }

            cleanTokens.Add(token);
        }

        var cleanTitle = string.Join(' ', cleanTokens).Trim(' ', '-', '.', '_');
        return cleanTitle;
    }
}
