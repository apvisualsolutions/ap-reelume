// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Identification;
using FsCheck;
using FsCheck.Fluent;
using Microsoft.FSharp.Core;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

public sealed class MediaNameParserProperties
{
    [Fact]
    public void Arbitrary_names_never_throw_and_retain_the_original()
    {
        var property = Prop.ForAll<NonNull<string>>(generated =>
        {
            var parser = new MediaNameParser();
            var input = generated.Get;

            var parsed = parser.Parse(new FileNameContext(input, []));

            Assert.Equal(input, parsed.OriginalName);
            Assert.NotNull(parsed.CleanTitle);
            Assert.NotNull(parsed.NoiseTags);
            Assert.NotNull(parsed.ParseWarnings);
        });

        Check.One(SeededConfig(314159, 271829), property);
    }

    [Fact]
    public void Out_of_range_episode_or_season_numbers_never_auto_classify()
    {
        var property = Prop.ForAll<PositiveInt>(generated =>
        {
            var parser = new MediaNameParser();
            var number = 1000 + (generated.Get % 9000);

            var parsedEpisode = parser.Parse(new FileNameContext($"Series.S01E{number}.mkv", ["Series", "Season 1"]));
            var parsedSeason = parser.Parse(new FileNameContext($"Series.S{100 + (number % 900)}E02.mkv", ["Series"]));

            Assert.NotEqual(ParsedMediaKind.Episode, parsedEpisode.Kind);
            Assert.Contains("NumberOutOfRange", parsedEpisode.ParseWarnings);
            Assert.NotEqual(ParsedMediaKind.Episode, parsedSeason.Kind);
            Assert.Contains("NumberOutOfRange", parsedSeason.ParseWarnings);
        });

        Check.One(SeededConfig(161803, 398875), property);
    }

    private static Config SeededConfig(ulong seedOne, ulong seedTwo)
    {
        var replay = new Replay(new Rnd(seedOne, seedTwo), FSharpOption<int>.None);
        return Config.QuickThrowOnFailure
            .WithMaxTest(10_000)
            .WithReplay(FSharpOption<Replay>.Some(replay));
    }
}
