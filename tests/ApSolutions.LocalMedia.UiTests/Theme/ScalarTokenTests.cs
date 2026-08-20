// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Every scalar the theme declares is spent by something, or it is named on a list that only shrinks.
/// </summary>
/// <remarks>
/// "Declared and never fed" is this repository's characteristic defect, and until now it was only
/// gated for services in the container. A scalar token has the same shape and none of the noise: it
/// costs nothing to add, nothing complains when nothing reads it, and by the time somebody looks
/// there is a parallel copy of a number that the application takes from somewhere else. That is
/// exactly what <c>MotionDurationStandardMilliseconds</c> was — a 160 beside
/// <c>FluentThemeService</c>'s own <c>TimeSpan.FromMilliseconds(160)</c>, with two separate tests
/// asserting about the copy — and what <c>SelectedStateGlyph</c> was, a dot declared once and
/// written literally in six view models.
///
/// <para>
/// Consumption counts in <b>any</b> <c>.axaml</c> under <c>src/</c>, the token file included: a
/// style that spends a scalar is spending it for real. The base theme can also consume one without
/// any file of ours naming it, so those are listed by name rather than guessed at — one today.
/// </para>
/// </remarks>
public sealed class ScalarTokenTests
{
    /// <summary>
    /// Scalars the base theme reads without any file of ours naming them. Named rather than
    /// inferred, because "something somewhere might use it" is how the list would stop meaning
    /// anything.
    /// </summary>
    private static readonly string[] SpentByTheBaseTheme = ["TextControlPlaceholderOpacity"];

    /// <summary>
    /// Declared for the redesign and not spent yet. <b>This list may only shrink, and on 2026-08-20
    /// it reached empty.</b>
    /// </summary>
    /// <remarks>
    /// It held the five space scalars from the day they were declared until the spacing phase spent
    /// them: all 186 spacing sites in <c>src/</c> now ask the scale. Empty is asserted rather than
    /// merely allowed, because a loop over an empty list passes by doing nothing, and a check that
    /// has gone blind looks exactly like one that has been satisfied. Adding a name back is a
    /// decision somebody has to make against a failing test.
    /// </remarks>
    private static readonly string[] NotSpentYet = [];

    /// <summary>
    /// What a resource dictionary holds that is not a scalar. Written as what to exclude rather than
    /// what to include, so a scalar of a type nobody has used yet is watched from the day it appears
    /// instead of being silently ignored.
    /// </summary>
    private static readonly HashSet<string> NotScalars = new(StringComparer.Ordinal)
    {
        "SolidColorBrush",
        "LinearGradientBrush",
        "Color",
        "StaticResource",
        "ResourceDictionary",
        "ControlTheme",
        "Style",
        "Styles",
    };

    [Fact]
    public void Every_declared_scalar_is_spent_or_named_on_a_list_that_only_shrinks()
    {
        var declared = DeclaredScalars();
        var spent = SpentScalars(declared);

        // Anti-blindness floor: if the parser ever stops finding the tokens, the gate would pass by
        // measuring nothing. These four are spent right now, in these counts, and a change to any of
        // them is a change somebody meant to make.
        Assert.True(
            declared.Count >= 8,
            $"only {declared.Count} scalars were found in the token file, so this gate is reading "
                + "the wrong thing rather than finding a tidy theme.");
        Assert.Contains("FocusStrokeThickness", spent);
        Assert.Contains("CornerRadiusSmall", spent);

        var unaccounted = declared
            .Where(name => !spent.Contains(name))
            .Where(name => !SpentByTheBaseTheme.Contains(name, StringComparer.Ordinal))
            .Where(name => !NotSpentYet.Contains(name, StringComparer.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unaccounted.Length == 0,
            $"{string.Join(", ", unaccounted)} — declared in the theme and read by no .axaml under "
                + "src/. Either spend it, or delete it, or add it to NotSpentYet and say when it "
                + "gets spent. A number declared twice is a number that will disagree with itself.");
    }

    [Fact]
    public void The_list_of_unspent_scalars_only_shrinks()
    {
        var declared = DeclaredScalars();
        var spent = SpentScalars(declared);

        // The list reached empty on 2026-08-20 and this says so out loud. Without it the loop below
        // would iterate over nothing and pass, which is what a check looks like after it has gone
        // blind — indistinguishable from one that is satisfied.
        Assert.True(
            NotSpentYet.Length == 0,
            $"{string.Join(", ", NotSpentYet)} — the unspent list was empty and is not any more. It "
                + "only shrinks, so a token that goes back on it is a phase that did not finish.");

        foreach (var name in NotSpentYet)
        {
            Assert.True(
                declared.Contains(name),
                $"{name} is on the unspent list and is not declared any more, so the list is "
                    + "describing a theme that no longer exists.");
            Assert.False(
                spent.Contains(name),
                $"{name} is on the unspent list and something now spends it. Take it off the list — "
                    + "that is the only direction this list moves.");
        }

        foreach (var name in SpentByTheBaseTheme)
        {
            Assert.True(
                declared.Contains(name),
                $"{name} is named as the base theme's and is not declared any more.");
            Assert.False(
                spent.Contains(name),
                $"{name} is named as the base theme's and one of our own files now reads it, so it "
                    + "belongs to us and not on that list.");
        }
    }

    /// <summary>No view writes a font size of its own instead of asking the scale.</summary>
    /// <remarks>
    /// Thirteen distinct literal sizes were in the tree across 52 uses and 30 files — 12, 14, 16, 17,
    /// 18, 20, 22, 24, 26, 28, 30, 32, 34 — which is not a scale, it is thirty files each deciding on
    /// their own. They map onto five tokens. This is the same shape of check
    /// <c>ReducedMotionTests</c> makes about durations, and for the same reason: a literal is
    /// invisible to anything that wants to change the whole application at once.
    /// <para>
    /// It counts what remains rather than what changed, so it stays true as views are added.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_view_writes_a_font_size_of_its_own_instead_of_asking_the_scale()
    {
        var literals = new List<string>();
        var references = 0;
        var root = Path.Combine(RepositoryLayout.Root, "src");
        foreach (var file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("FontSize=\"", StringComparison.Ordinal)
                    && !trimmed.Contains(" FontSize=\"", StringComparison.Ordinal))
                {
                    continue;
                }

                if (trimmed.Contains("FontSize=\"{", StringComparison.Ordinal))
                {
                    references++;
                }
                else
                {
                    literals.Add($"{Path.GetFileName(file)}: {trimmed}");
                }
            }
        }

        // Anti-blindness floor: a reader that found nothing would pass by measuring nothing. Fifty-two
        // uses were mapped, and a view added later only makes this bigger.
        Assert.True(
            references >= 52,
            $"only {references} font sizes come from the scale, which is fewer than the 52 that were "
                + "mapped onto it, so either this check stopped reading or sizes went back to being "
                + "written by hand.");

        Assert.True(
            literals.Count == 0,
            "a view writes its own font size instead of asking the scale:\n  "
                + string.Join("\n  ", literals));
    }

    /// <summary>No view writes a spacing of its own instead of asking the scale.</summary>
    /// <remarks>
    /// <para>
    /// The same check the font sizes get, for the same reason, and this is the half that the update
    /// screen taught: a test that compares the painted <em>value</em> cannot tell a literal from a
    /// token while the two agree — and they agree exactly when the tokenisation would be correct, so
    /// the false green is the normal case. What is asserted is that the markup does not write the
    /// number.
    /// </para>
    /// <para>
    /// All five spacing properties are read, not just <c>Spacing</c>. <c>RowSpacing</c> and
    /// <c>ColumnSpacing</c> belong to <c>Grid</c> and are the same double saying the same thing, and
    /// they were nearly missed: a pattern anchored with a word boundary counted 163 sites where there
    /// were 186, and the 23 it skipped were all of them. A gate that watches four of five properties
    /// is a gate that says the phase is finished when it is not.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_view_writes_a_spacing_of_its_own_instead_of_asking_the_scale()
    {
        var literals = new List<string>();
        var references = 0;
        var pattern = new Regex(
            @"(Row|Column|Item|Line)?Spacing=""(?<value>[^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepositoryLayout.Root, "src"),
            "*.axaml",
            SearchOption.AllDirectories))
        {
            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                var value = match.Groups["value"].Value;
                if (value.StartsWith('{'))
                {
                    references++;
                }
                else
                {
                    literals.Add($"{Path.GetFileName(file)}: {match.Value}");
                }
            }
        }

        // The literals go first because this is the assertion that names the file. The floor below
        // catches the same mutation, but it can only say that a number is missing, not where it went.
        Assert.True(
            literals.Count == 0,
            "a view writes its own spacing instead of asking the scale:\n  "
                + string.Join("\n  ", literals));

        // Anti-blindness floor: a reader that found nothing would pass by measuring nothing. 186 sites
        // were mapped onto the scale, and a view added later only makes this bigger.
        Assert.True(
            references >= 186,
            $"only {references} spacings come from the scale, which is fewer than the 186 that were "
                + "mapped onto it, so either this check stopped reading or spacings went back to "
                + "being written by hand.");
    }

    /// <summary>No view writes a corner radius of its own instead of asking the scale.</summary>
    /// <remarks>
    /// <para>
    /// The third scale, and the last one that was still loose. Thirty literals across twenty-six
    /// files: 8 eighteen times, 4 five times, and then 6 four times, 10 twice and 12 once.
    /// </para>
    /// <para>
    /// Those last seven looked like a missing step — the 10s and the 12 are all card surfaces, and a
    /// large card sharing a button's radius does look wrong — so the hypothesis was worth having. The
    /// measurement refused it: of the seven surfaces painted with <c>CardSurfaceBrush</c>, <b>four
    /// already carried 8</b> and three carried 10 or 12. That is not a step the tree is asking for,
    /// it is a split nobody decided. So all seven go to <c>CornerRadiusMedium</c>, which is what four
    /// of the cards already were, and the scale stays at two values with no gap between them. Only
    /// one site moves by more than 2px, and it moves into line with six others.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_view_writes_a_corner_radius_of_its_own_instead_of_asking_the_scale()
    {
        var literals = new List<string>();
        var references = 0;
        var pattern = new Regex(
            @"CornerRadius=""(?<value>[^""]+)""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));

        foreach (var file in Directory.EnumerateFiles(
            Path.Combine(RepositoryLayout.Root, "src"),
            "*.axaml",
            SearchOption.AllDirectories))
        {
            foreach (Match match in pattern.Matches(File.ReadAllText(file)))
            {
                if (match.Groups["value"].Value.StartsWith('{'))
                {
                    references++;
                }
                else
                {
                    literals.Add($"{Path.GetFileName(file)}: {match.Value}");
                }
            }
        }

        Assert.True(
            literals.Count == 0,
            "a view writes its own corner radius instead of asking the scale:\n  "
                + string.Join("\n  ", literals));

        // Anti-blindness floor: 37 corners come from the scale, and a view added later only makes
        // this bigger.
        Assert.True(
            references >= 37,
            $"only {references} corner radii come from the scale, which is fewer than the 37 that "
                + "were mapped onto it, so either this check stopped reading or radii went back to "
                + "being written by hand.");
    }

    /// <summary>Every keyed entry of the token file that is not a brush or a redirect.</summary>
    private static HashSet<string> DeclaredScalars()
    {
        var key = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return XDocument.Load(TokenPath())
            .Descendants()
            .Where(element => element.Attribute(key) is not null)
            .Where(element => !NotScalars.Contains(element.Name.LocalName))
            .Select(element => element.Attribute(key)!.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Which of them any <c>.axaml</c> under <c>src/</c> asks for, by every form that asks: the two
    /// markup extensions and the redirect entry the theme files are built out of.
    /// </summary>
    private static HashSet<string> SpentScalars(HashSet<string> declared)
    {
        var spent = new HashSet<string>(StringComparer.Ordinal);
        var root = Path.Combine(RepositoryLayout.Root, "src");
        foreach (var file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var name in declared)
            {
                if (text.Contains($"DynamicResource {name}}}", StringComparison.Ordinal)
                    || text.Contains($"StaticResource {name}}}", StringComparison.Ordinal)
                    || text.Contains($"ResourceKey=\"{name}\"", StringComparison.Ordinal))
                {
                    _ = spent.Add(name);
                }
            }
        }

        return spent;
    }

    private static string TokenPath() => Path.Combine(
        RepositoryLayout.Root,
        "src",
        "ApSolutions.LocalMedia.Presentation",
        "Theme",
        "DesignTokens.axaml");
}
