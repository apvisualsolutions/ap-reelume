// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The one line a row of the player's side column has room for, branch by branch.
/// </summary>
/// <remarks>
/// <c>SideListRowTests</c> asserts that the four lists use this and what the screen ends up saying.
/// What is here is the rest of the converter: the clock that grows an hour when there is one, the
/// value it was never given, and the direction it does not go in. A converter whose only cover is the
/// happy path through a view is a converter whose fallbacks nobody has ever run.
/// </remarks>
public sealed class MarkerRowLabelConverterTests
{
    private static readonly SeriesId Series = new(Guid.Parse("d1f70001-0000-4000-8000-000000000001"));

    private static readonly MediaFileId FileId = new(Guid.Parse("c2d40001-0000-4000-8000-00000000000a"));

    /// <summary>
    /// Under an hour the clock has no hour in it, and over one it does.
    /// </summary>
    /// <remarks>
    /// Both sides of the boundary, because a format string that always wrote the hour would read
    /// "0:00:30" for the first thirty seconds of a film and one that never did would read "61:40" for
    /// an hour and two minutes — and either passes a test that only measures the other side.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(30, 120, "0:30–2:00")]
    [InlineData(2_800, 3_000, "46:40–50:00")]
    [InlineData(3_700, 3_805, "1:01:40–1:03:25")]
    public void The_clock_grows_an_hour_only_when_there_is_one(int start, int end, string expected)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var label = Convert(Marker(MarkerKind.Intro, start, end));

        Assert.Equal($"{Resource("MarkerKindIntro")} · {expected}", label);
    }

    /// <summary>
    /// A detection says it is confirmed, and a marker never does.
    /// </summary>
    /// <remarks>
    /// A manual range is not a detection anybody confirmed — it is one somebody wrote — so the suffix
    /// would be describing a flag the record does not carry. Asserted on both types because the two
    /// share this method and only one of them has the field.
    /// </remarks>
    [AvaloniaFact]
    public void Only_a_detection_can_say_it_was_confirmed()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var confirmed = Resource("DetectedMarkerReviewConfirmed");

        Assert.DoesNotContain(confirmed, Convert(Marker(MarkerKind.Intro, 30, 120)), StringComparison.Ordinal);
        Assert.DoesNotContain(
            confirmed,
            Convert(Detection(MarkerKind.Intro, 30, 120, confirmed: false)),
            StringComparison.Ordinal);
        Assert.EndsWith(
            $" · {confirmed}",
            Convert(Detection(MarkerKind.Intro, 30, 120, confirmed: true)),
            StringComparison.Ordinal);
    }

    /// <summary>The kind on its own, which is what the picker binds.</summary>
    [AvaloniaFact]
    public void A_kind_on_its_own_is_just_its_word()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        foreach (var kind in Enum.GetValues<MarkerKind>())
        {
            Assert.Equal(Resource("MarkerKind" + kind), Convert(kind));
        }
    }

    /// <summary>
    /// Anything else is nothing, rather than the type's own name.
    /// </summary>
    /// <remarks>
    /// Which is the whole reason this class exists: a converter that fell through to
    /// <c>value.ToString()</c> would put back exactly the record dump it was written to remove, and it
    /// would do it the day somebody bound a fifth thing to it.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("a string")]
    [InlineData(7)]
    public void Anything_it_was_not_given_converts_to_nothing(object? value)
    {
        Assert.Equal(string.Empty, Convert(value));
    }

    /// <summary>
    /// A kind whose words nobody wrote shows its key, and so does one whose key holds something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both are reachable rather than defensive: <c>MarkerKind</c> can gain a fourth value in a commit
    /// that forgets the two dictionaries, and a key can be redefined as something that is not words.
    /// In either case the row says <c>MarkerKind99</c> — ugly, reported within a day, and far better
    /// than a row that has quietly lost the only thing saying which range it is.
    /// </para>
    /// <para>
    /// The second half is arranged rather than waited for: the key is put into the application's own
    /// dictionary holding a number, and taken out again, so the assertion is about the branch and not
    /// about the state some other test happened to leave behind.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_kind_with_no_words_behind_it_shows_its_key()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var resources = Avalonia.Application.Current!.Resources;

        Assert.Equal("MarkerKind98", Convert((MarkerKind)98));

        resources.Add("MarkerKind99", 123);
        try
        {
            Assert.Equal("MarkerKind99", Convert((MarkerKind)99));
        }
        finally
        {
            resources.Remove("MarkerKind99");
        }
    }

    /// <summary>A label is read out of a range and never written back into one.</summary>
    [Fact]
    public void The_conversion_does_not_go_the_other_way()
    {
        Assert.Throws<NotSupportedException>(() => new MarkerRowLabelConverter().ConvertBack(
            "Introducción · 0:30–2:00",
            typeof(IntroMarker),
            parameter: null,
            CultureInfo.InvariantCulture));
    }

    private static string Convert(object? value) =>
        Assert.IsType<string>(new MarkerRowLabelConverter().Convert(
            value,
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture));

    private static IntroMarker Marker(MarkerKind kind, int start, int end) =>
        new(
            Guid.Parse("11110001-0000-4000-8000-000000000001"),
            Series,
            kind,
            TimeSpan.FromSeconds(start),
            TimeSpan.FromSeconds(end),
            MarkerOrigin.Manual,
            null,
            false);

    private static DetectedMarker Detection(MarkerKind kind, int start, int end, bool confirmed) =>
        new(
            Guid.Parse("22220001-0000-4000-8000-000000000001"),
            Series,
            FileId,
            kind,
            TimeSpan.FromSeconds(start),
            TimeSpan.FromSeconds(end),
            0.87,
            3,
            confirmed);

    private static string Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        return Assert.IsType<string>(value);
    }
}
