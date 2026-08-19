// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
    /// Declared for the redesign and not spent yet. <b>This list may only shrink.</b> The views are
    /// the phase that spends them, and the test below refuses both a name that has started being
    /// used and a name that has stopped existing, so it cannot quietly become a place to park things.
    /// </summary>
    private static readonly string[] NotSpentYet =
    [
        "SpaceXSmall",
        "SpaceSmall",
        "SpaceMedium",
        "SpaceLarge",
        "SpaceXLarge",
        "CornerRadiusMedium",
    ];

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
