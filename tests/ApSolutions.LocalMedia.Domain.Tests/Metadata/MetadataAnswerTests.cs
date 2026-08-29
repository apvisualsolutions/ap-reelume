// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

/// <summary>
/// What a metadata provider hands back. The two shapes it answers with — a search result and a set
/// of details — each carry the language they were actually fetched in, which is not the same as the
/// language that was asked for: MetadataLanguage offers a fallback, so an answer may arrive in the
/// second choice, and only the answer itself knows which.
/// </summary>
public sealed class MetadataAnswerTests
{
    private static readonly MetadataReference Reference =
        new("tmdb", "movie:329865", MetadataContentKind.Movie);

    /// <summary>
    /// A fallback is a different language, not a retry of the same one. OrderedValues is what decides
    /// how many requests a lookup may make, and it must not spend a second one asking the provider
    /// the same question — a fallback that only differs in case is the same language written twice.
    /// </summary>
    [Theory]
    [InlineData("es-ES", "en-US", 2)]
    [InlineData("es-ES", "es-es", 1)]
    [InlineData("es-ES", null, 1)]
    [InlineData("es-ES", "  ", 1)]
    public void A_fallback_is_only_a_second_request_when_it_is_a_second_language(
        string primary,
        string? fallback,
        int expected)
    {
        var ordered = new MetadataLanguage(primary, fallback).OrderedValues();

        Assert.Equal(expected, ordered.Count);
        Assert.Equal(primary, ordered[0]);
    }

    [Fact]
    public void A_language_with_no_primary_is_refused_rather_than_asked_for()
    {
        Assert.Throws<ArgumentException>(() => new MetadataLanguage("  ", "en-US").OrderedValues());
    }

    /// <summary>
    /// Both answers record the language they came back in. Nothing in src/ reads it back today — the
    /// cache keys on the language that was requested, not on the one that answered — so this is the
    /// contract as the provider fills it, and the asymmetry is worth knowing about: a lookup that
    /// fell through to its fallback is stored under the language nobody got.
    /// </summary>
    [Fact]
    public void Both_answers_say_which_language_they_came_back_in()
    {
        var result = new MetadataSearchResult(Reference, "en-US", "Arrival", "Arrival", 2016);
        var details = new MetadataDetails(
            Reference,
            "en-US",
            "Arrival",
            "Arrival",
            "Overview",
            2016,
            ["Drama"],
            "/poster.jpg",
            "/backdrop.jpg",
            "abc");

        Assert.Equal("en-US", result.Language);
        Assert.Equal("en-US", details.Language);
        Assert.Equal(Reference, result.Reference);
        Assert.Equal(MetadataContentKind.Movie, details.Reference.Kind);
    }
}
