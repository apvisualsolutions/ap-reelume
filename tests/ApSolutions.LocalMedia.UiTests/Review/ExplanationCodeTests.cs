// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Review;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Review;

/// <summary>
/// The words behind the codes that say why a file was matched to a title.
/// </summary>
/// <remarks>
/// <para>
/// Measured on 2026-08-21: <c>CandidateCardView</c> painted <c>ExplanationCodes</c> with a bare
/// <c>Text="{Binding}"</c>, and those codes are dotted internal paths —
/// <c>Identification.Error.KindConflict</c>, <c>Identification.Signal.Title</c>. Eleven of them exist
/// in the domain and <b>none had a string in either language</b>. That is the heart of this screen: the
/// explanation is the answer to "why do you think this file is this film", and it answered with a
/// namespace.
/// </para>
/// <para>
/// The tree already solves it one view away. <c>ResourceKeyConverter</c> exists for exactly this, and
/// <c>RecommendationsRailView</c> paints its recommendation reasons through it — the same kind of data,
/// translated there and raw here.
/// </para>
/// <para>
/// The first test scans the source rather than a list kept here, in the shape this repository already
/// uses for undeclared network hosts: <b>a twelfth code cannot be born raw</b>, because the day it is
/// written into the domain this fails until its words exist in both dictionaries.
/// </para>
/// </remarks>
public sealed class ExplanationCodeTests
{
    /// <summary>Every dotted identification code any source file writes.</summary>
    /// <remarks>
    /// The <c>(?&lt;!cref=)</c> is not decoration and it loosens nothing. A cross-reference in
    /// documentation is a quoted string that starts with the same word, and on 2026-08-28 one of
    /// them made this fail asking for a dictionary entry for a class name. That is the shape ARQ-013
    /// fixed seen from the other side: a gate reading source as text and believing a comment. What
    /// it still catches is what it is for — a code literal written into the domain with no words
    /// behind it.
    /// </remarks>
    private static readonly Regex CodeLiteral = new(
        "(?<!cref=)\"(Identification\\.[A-Za-z.]+)\"",
        RegexOptions.Compiled);

    /// <summary>
    /// Every code the source declares has words in both languages, and the two differ.
    /// </summary>
    /// <remarks>
    /// The count is asserted first: a scan that found nothing would pass the loop below without
    /// measuring a single code, which is the way this shape of test goes blind.
    /// </remarks>
    [AvaloniaFact]
    public void Every_identification_code_in_the_source_has_words_in_both_languages()
    {
        var codes = DeclaredCodes();
        Assert.True(codes.Length >= 11, $"the scan found {codes.Length} codes, so it is looking in the wrong place.");

        Assert.NotNull(Avalonia.Application.Current);
        var byLanguage = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var culture in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(culture));
            var words = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var code in codes)
            {
                Assert.True(
                    Avalonia.Application.Current!.TryFindResource(Key(code), out var value),
                    $"{code} is painted on the review screen and {Key(code)} is not declared, "
                        + $"so somebody reviewing a match reads the code path in {culture}.");
                words[code] = Assert.IsType<string>(value);
            }

            byLanguage[culture] = words;
        }

        var untranslated = codes
            .Where(code => string.Equals(byLanguage["es-ES"][code], byLanguage["en-US"][code], StringComparison.Ordinal))
            .ToArray();
        Assert.True(
            untranslated.Length == 0,
            $"these say the same in both languages: {string.Join(", ", untranslated)}.");
    }

    /// <summary>
    /// The card paints the words and never the code, by sight and by ear.
    /// </summary>
    /// <remarks>
    /// Both halves. The visible text is what a person reads; <c>HelpText</c> is what a screen reader
    /// announces, and it joined the raw codes — so the ear had it worse than the eye.
    /// </remarks>
    [AvaloniaFact]
    public void The_candidate_card_says_the_words_and_never_the_code()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var codes = new[] { "Identification.Signal.Title", "Identification.Warning.AmbiguousName" };
        var view = new CandidateCardView { DataContext = new CandidateCardViewModel(Candidate(codes)) };
        var window = new Window { Width = 620, Height = 400, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var painted = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToArray();

        foreach (var code in codes)
        {
            var words = Assert.IsType<string>(
                Avalonia.Application.Current!.TryFindResource(Key(code), out var value) ? value : null);
            Assert.Contains(words, painted, StringComparer.Ordinal);
            Assert.DoesNotContain(code, painted, StringComparer.Ordinal);
        }

        var announced = view.GetVisualDescendants()
            .OfType<Control>()
            .Select(AutomationProperties.GetHelpText)
            .Concat([AutomationProperties.GetHelpText(view)])
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        Assert.NotEmpty(announced);
        Assert.All(announced, text => Assert.DoesNotContain("Identification.", text!, StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    /// The key that holds one code's words, which is the code.
    /// </summary>
    /// <remarks>
    /// Identity and not a mapping. The converter takes the value it is given as the key, so any
    /// transformation would be a second place where the same name is written, and the two would
    /// diverge the first time somebody renamed a code.
    /// </remarks>
    private static string Key(string code) => code;

    private static string[] DeclaredCodes() =>
        [.. Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => CodeLiteral.Matches(File.ReadAllText(file)).Select(match => match.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private static MatchCandidate Candidate(IReadOnlyList<string> codes) =>
        new(
            new CandidateId(Guid.Parse("55550001-0000-4000-8000-000000000001")),
            new MediaFileId(Guid.Parse("55550002-0000-4000-8000-000000000002")),
            "movie:603",
            CandidateContentKind.Movie,
            0.91,
            ScoringModelVersion: 1,
            ReviewState.Suggested,
            Signals: [],
            codes);
}
