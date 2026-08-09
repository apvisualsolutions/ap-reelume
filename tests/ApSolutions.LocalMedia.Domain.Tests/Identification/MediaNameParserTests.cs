using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

public sealed class MediaNameParserTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TheoryData<MediaNameCase> ApprovedCases
    {
        get
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "media-name-cases.json");
            var cases = JsonSerializer.Deserialize<MediaNameCase[]>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException("The approved media-name fixture is empty.");
            var data = new TheoryData<MediaNameCase>();
            foreach (var item in cases)
            {
                data.Add(item);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(ApprovedCases))]
    public void Approved_corpus_is_parsed_into_explainable_media_signals(MediaNameCase fixture)
    {
        var parser = new MediaNameParser();

        var parsed = parser.Parse(new FileNameContext(fixture.FileName, fixture.Folders));

        Assert.Equal(fixture.FileName, parsed.OriginalName);
        Assert.Equal(Enum.Parse<ParsedMediaKind>(fixture.Kind), parsed.Kind);
        Assert.Equal(fixture.CleanTitle, parsed.CleanTitle);
        Assert.Equal(fixture.Year, parsed.Year);
        Assert.Equal(fixture.Season, parsed.Season);
        Assert.Equal(fixture.Episode, parsed.Episode);
        Assert.Equal(fixture.AbsoluteEpisode, parsed.AbsoluteEpisode);
        Assert.Equal(fixture.IsSpecial, parsed.IsSpecial);
        Assert.Equal(fixture.NoiseTags, parsed.NoiseTags);
        Assert.Equal(fixture.Warnings, parsed.ParseWarnings);
    }

    [Fact]
    public void Compact_episode_without_supporting_season_context_requires_review()
    {
        var parser = new MediaNameParser();

        var parsed = parser.Parse(new FileNameContext("Serie.Cap.803.mkv", ["Serie"]));

        Assert.Equal(ParsedMediaKind.Unknown, parsed.Kind);
        Assert.Null(parsed.Season);
        Assert.Null(parsed.Episode);
        Assert.Contains("AmbiguousCompactEpisode", parsed.ParseWarnings);
    }

    [Fact]
    public void Long_unicode_path_is_bounded_and_preserves_the_source_name()
    {
        var parser = new MediaNameParser();
        var fileName = $"{new string('界', 270)}.S01E02.[1080p].mkv";

        var started = DateTime.UtcNow;
        var parsed = parser.Parse(new FileNameContext(fileName, ["長いシリーズ", "Season 1"]));

        Assert.Equal(fileName, parsed.OriginalName);
        Assert.Equal(ParsedMediaKind.Episode, parsed.Kind);
        Assert.Equal(1, parsed.Season);
        Assert.Equal(2, parsed.Episode);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
    }

    public sealed record MediaNameCase
    {
        public required string FileName { get; init; }

        public string[] Folders { get; init; } = [];

        public required string Kind { get; init; }

        public required string CleanTitle { get; init; }

        public int? Year { get; init; }

        public int? Season { get; init; }

        public int? Episode { get; init; }

        public int? AbsoluteEpisode { get; init; }

        public bool IsSpecial { get; init; }

        public string[] NoiseTags { get; init; } = [];

        public string[] Warnings { get; init; } = [];
    }
}
