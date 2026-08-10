// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

public sealed class DuplicateGroupingPolicyTests
{
    [Fact]
    public void Two_5x10_files_are_grouped_as_visible_versions_without_delete_or_hide_actions()
    {
        var parser = new MediaNameParser();
        var policy = new DuplicateGroupingPolicy();
        var firstId = new MediaFileId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var secondId = new MediaFileId(Guid.Parse("30000000-0000-0000-0000-000000000002"));
        var first = parser.Parse(new FileNameContext("Show.5x10.1080p.mkv", ["Show", "Season 5"]));
        var second = parser.Parse(new FileNameContext("Show.5x10.2160p.mkv", ["Show", "Season 5"]));

        var decision = policy.Assess([
            new DuplicateFileMatch(firstId, "tv:show:s05e10", first),
            new DuplicateFileMatch(secondId, "tv:show:s05e10", second),
        ]);

        Assert.True(decision.CanGroup);
        Assert.False(decision.RequiresConfirmation);
        Assert.Equal([firstId, secondId], decision.VisibleFileIds);
        Assert.Equal("Identification.Duplicate.SameEpisode", decision.ReasonCode);
        var publicNames = typeof(DuplicateGroupingDecision).GetMembers().Select(member => member.Name);
        Assert.DoesNotContain(publicNames, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicNames, name => name.Contains("Hide", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Different_episode_or_content_key_is_never_auto_grouped()
    {
        var parser = new MediaNameParser();
        var policy = new DuplicateGroupingPolicy();
        var firstId = new MediaFileId(Guid.NewGuid());
        var secondId = new MediaFileId(Guid.NewGuid());

        var decision = policy.Assess([
            new DuplicateFileMatch(
                firstId,
                "tv:show:s05e10",
                parser.Parse(new FileNameContext("Show.5x10.mkv", ["Show", "Season 5"]))),
            new DuplicateFileMatch(
                secondId,
                "tv:other:s05e11",
                parser.Parse(new FileNameContext("Other.5x11.mkv", ["Other", "Season 5"]))),
        ]);

        Assert.False(decision.CanGroup);
        Assert.True(decision.RequiresConfirmation);
        Assert.Equal([firstId, secondId], decision.VisibleFileIds);
    }

    [Fact]
    public void Same_movie_content_is_grouped_without_hiding_either_file()
    {
        var parser = new MediaNameParser();
        var policy = new DuplicateGroupingPolicy();
        var firstId = new MediaFileId(Guid.NewGuid());
        var secondId = new MediaFileId(Guid.NewGuid());
        var parsed = parser.Parse(new FileNameContext("Arrival.2016.mkv", []));

        var decision = policy.Assess([
            new DuplicateFileMatch(firstId, "tmdb:movie:329865", parsed),
            new DuplicateFileMatch(secondId, "tmdb:movie:329865", parsed),
        ]);

        Assert.True(decision.CanGroup);
        Assert.False(decision.RequiresConfirmation);
        Assert.Equal("Identification.Duplicate.SameMovie", decision.ReasonCode);
        Assert.Equal([firstId, secondId], decision.VisibleFileIds);
    }

    [Fact]
    public void Unknown_names_are_kept_for_review_even_with_the_same_content_key()
    {
        var parser = new MediaNameParser();
        var policy = new DuplicateGroupingPolicy();
        var parsed = parser.Parse(new FileNameContext("???..mkv", []));

        var decision = policy.Assess([
            new DuplicateFileMatch(new MediaFileId(Guid.NewGuid()), "local:unknown", parsed),
            new DuplicateFileMatch(new MediaFileId(Guid.NewGuid()), "local:unknown", parsed),
        ]);

        Assert.False(decision.CanGroup);
        Assert.True(decision.RequiresConfirmation);
        Assert.Equal("Identification.Duplicate.NeedsReview", decision.ReasonCode);
    }

    [Fact]
    public void Null_or_single_file_input_is_rejected()
    {
        var policy = new DuplicateGroupingPolicy();

        Assert.Throws<ArgumentNullException>(() => policy.Assess(null!));
        Assert.Throws<ArgumentException>(() => policy.Assess([
            new DuplicateFileMatch(
                new MediaFileId(Guid.NewGuid()),
                "local:one",
                new MediaNameParser().Parse(new FileNameContext("One.2020.mkv", []))),
        ]));
    }
}
