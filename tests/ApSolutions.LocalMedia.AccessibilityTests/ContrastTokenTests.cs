// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// The contrast of what the application paints, read from the dictionaries it paints it from.
/// </summary>
/// <remarks>
/// This used to measure a parallel list of <c>Color</c> resources kept beside the theme
/// dictionaries, and nothing else in the tree read that list. It had already drifted: the colours
/// said the control border was <c>#475569</c> while the dictionary painted <c>#64748B</c>, and they
/// described a HighContrastLight mode that no dictionary provided, so the check passed for a theme
/// the application could not show. A measurement of something nobody paints is not a measurement.
/// </remarks>
public sealed class ContrastTokenTests
{
    private const double TextMinimum = 4.5;
    private const double NonTextMinimum = 3.0;

    private static readonly string[] ThemeNames =
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    // Every dictionary carries every key, so a theme cannot quietly fall back to whatever the base
    // Fluent theme draws — which no contrast check would ever see.
    private static readonly string[] RequiredKeys =
    [
        "ShellSurfaceBrush",
        "PlayerSurfaceBrush",
        "NavigationSurfaceBrush",
        "CardSurfaceBrush",
        "ControlFillBrush",
        "ControlFillHoverBrush",
        "ControlFillPressedBrush",
        "ControlFillDisabledBrush",
        "ShellBorderBrush",
        "ShellHairlineBrush",
        "TextPrimaryBrush",
        "TextSecondaryBrush",
        "TextDisabledBrush",
        "ControlTextActiveBrush",
        "FocusStrokeBrush",
        "FocusInnerStrokeBrush",
        "AccentBrush",
        "AccentSubtleBrush",
        "AccentTextBrush",
        "WarningSurfaceBrush",
        "WarningBorderBrush",
        "DangerSurfaceBrush",
        "DangerBorderBrush",
        "PositiveSurfaceBrush",
        "PositiveBorderBrush",
    ];

    [Fact]
    public void Every_visual_mode_carries_every_brush()
    {
        var themes = LoadThemeBrushes();

        Assert.Equal(
            ThemeNames.Order(StringComparer.Ordinal),
            themes.Keys.Order(StringComparer.Ordinal));
        foreach (var theme in ThemeNames)
        {
            Assert.Equal(
                RequiredKeys.Order(StringComparer.Ordinal),
                themes[theme].Keys.Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Text_control_and_focus_tokens_meet_WCAG_AA_across_four_visual_modes()
    {
        var themes = LoadThemeBrushes();

        foreach (var theme in ThemeNames)
        {
            var brushes = themes[theme];

            // Body text, on every surface it can land on.
            foreach (var surface in new[]
            {
                "ShellSurfaceBrush",
                "NavigationSurfaceBrush",
                "CardSurfaceBrush",
                "ControlFillBrush",
                "ControlFillDisabledBrush",
                "WarningSurfaceBrush",
                "DangerSurfaceBrush",
                "PositiveSurfaceBrush",
            })
            {
                AssertContrastAtLeast(brushes, "TextPrimaryBrush", surface, TextMinimum, $"{theme} primary text on {surface}");
            }

            AssertContrastAtLeast(brushes, "TextSecondaryBrush", "ShellSurfaceBrush", TextMinimum, $"{theme} secondary text");
            AssertContrastAtLeast(brushes, "TextSecondaryBrush", "CardSurfaceBrush", TextMinimum, $"{theme} secondary text on a card");

            // Disabled text is exempt from 1.4.3, which is exactly why it goes unmeasured and ends up
            // illegible. It is held to the non-text bar rather than to nothing.
            AssertContrastAtLeast(brushes, "TextDisabledBrush", "ControlFillDisabledBrush", NonTextMinimum, $"{theme} disabled text");

            // What makes a control a control: its boundary, wherever it sits.
            AssertContrastAtLeast(brushes, "ShellBorderBrush", "ControlFillBrush", NonTextMinimum, $"{theme} control boundary");
            AssertContrastAtLeast(brushes, "ShellBorderBrush", "ShellSurfaceBrush", NonTextMinimum, $"{theme} control boundary on the shell");

            // The focus ring against what it is drawn over, and its two rings against each other:
            // the double ring is a geometry cue, and two rings the same colour are one ring.
            AssertContrastAtLeast(brushes, "FocusStrokeBrush", "ShellSurfaceBrush", NonTextMinimum, $"{theme} focus on the shell");
            AssertContrastAtLeast(brushes, "FocusStrokeBrush", "ControlFillBrush", NonTextMinimum, $"{theme} focus on a control");
            AssertContrastAtLeast(brushes, "FocusInnerStrokeBrush", "FocusStrokeBrush", NonTextMinimum, $"{theme} focus inner ring");

            // The accent has to be visible as itself, and it has to be distinguishable from focus.
            AssertContrastAtLeast(brushes, "AccentBrush", "ShellSurfaceBrush", NonTextMinimum, $"{theme} accent");

            // And whatever sits on the accent has to be readable on it. This one is measured because
            // guessing it went wrong once already: the decision written down said white in light,
            // dark and high contrast light and black in high contrast dark — but the dark theme's
            // accent is a pale blue, and white on it reads 2.40:1. The colour follows the accent's
            // luminance, not the theme's name.
            AssertContrastAtLeast(brushes, "AccentTextBrush", "AccentBrush", TextMinimum, $"{theme} text on the accent");
            Assert.False(
                string.Equals(brushes["AccentBrush"], brushes["FocusStrokeBrush"], StringComparison.OrdinalIgnoreCase),
                $"{theme} paints the accent and the focus ring in the same colour, so the mark and the "
                    + "keyboard's position are indistinguishable — which is worst in the theme where "
                    + "focus matters most.");

            // Warning, danger and positive: the border is what separates the strip from the page.
            foreach (var state in new[] { "Warning", "Danger", "Positive" })
            {
                AssertContrastAtLeast(
                    brushes,
                    $"{state}BorderBrush",
                    $"{state}SurfaceBrush",
                    NonTextMinimum,
                    $"{theme} {state.ToLowerInvariant()} boundary");
            }
        }
    }

    [Fact]
    public void High_contrast_tells_the_three_states_apart_without_using_colour()
    {
        var themes = LoadThemeBrushes();

        foreach (var theme in new[] { "HighContrastLight", "HighContrastDark" })
        {
            var brushes = themes[theme];
            foreach (var state in new[] { "Warning", "Danger", "Positive" })
            {
                Assert.Equal(brushes["ShellSurfaceBrush"], brushes[$"{state}SurfaceBrush"]);
                Assert.Equal(brushes["ShellBorderBrush"], brushes[$"{state}BorderBrush"]);
            }
        }
    }

    [Fact]
    public void Focus_is_a_double_ring_of_at_least_two_pixels_and_selected_state_has_a_non_color_cue()
    {
        var resources = LoadScalars();
        var thickness = ParseThickness(resources["FocusStrokeThickness"]);
        var innerThickness = ParseThickness(resources["FocusInnerStrokeThickness"]);

        Assert.True(thickness >= 2.0, $"Focus thickness was {thickness}.");
        Assert.True(innerThickness >= 1.0, $"Focus inner thickness was {innerThickness}.");

        var document = XDocument.Load(GetTokenPath());
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "Setter"),
            setter => setter.Attribute("Property")?.Value == "BorderThickness"
                && setter.Attribute("Value")?.Value.Contains("FocusStrokeThickness", StringComparison.Ordinal) is true);

        var appearancePath = System.IO.Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Settings",
            "AppearanceSettingsView.axaml");
        var appearance = XDocument.Load(appearancePath);
        var stateCueBindings = appearance.Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName == "Text"
                && attribute.Value.Contains("StateCue", StringComparison.Ordinal))
            .ToArray();
        // Three theme choices plus the two language choices BUG-011 added; every option carries
        // its non-color cue.
        Assert.Equal(5, stateCueBindings.Length);
    }

    [Fact]
    public void Mica_high_contrast_and_Windows_motion_detection_are_isolated_to_the_Windows_host()
    {
        var repositoryRoot = RepositoryLayout.Root;
        var presentationRoot = System.IO.Path.Combine(
            repositoryRoot,
            "src",
            "ApSolutions.LocalMedia.Presentation");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories),
            file => File.ReadAllText(file).Contains("MicaBackdropService", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories),
            file => File.ReadAllText(file).Contains("SystemParametersInfo", StringComparison.Ordinal));

        var presentation = Assembly.Load("ApSolutions.LocalMedia.Presentation");
        var windows = Assembly.Load("ApSolutions.LocalMedia.Windows");
        var backdropContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IBackdropService");
        var reducedMotionContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IReducedMotionService");
        var highContrastContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IHighContrastService");
        var mica = RequireType(
            windows,
            "ApSolutions.LocalMedia.Windows.Windowing.MicaBackdropService");
        var reducedMotion = RequireType(
            windows,
            "ApSolutions.LocalMedia.Windows.Accessibility.WindowsReducedMotionService");
        var highContrast = RequireType(
            windows,
            "ApSolutions.LocalMedia.Windows.Accessibility.WindowsHighContrastService");

        Assert.True(backdropContract.IsAssignableFrom(mica));
        Assert.True(reducedMotionContract.IsAssignableFrom(reducedMotion));
        Assert.True(highContrastContract.IsAssignableFrom(highContrast));
        Assert.Equal("ShellSurfaceBrush", mica.GetProperty("SolidFallbackResourceKey")?.GetValue(null));
    }

    private static Dictionary<string, Dictionary<string, string>> LoadThemeBrushes()
    {
        var path = GetTokenPath();
        Assert.True(File.Exists(path), $"Design token dictionary is missing: {path}");
        var document = XDocument.Load(path);
        var dictionaries = document.Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary.ThemeDictionaries")
            .SelectMany(element => element.Elements())
            .ToArray();

        var themes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var dictionary in dictionaries)
        {
            var key = dictionary.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value;
            // Longest first: "{x:Static theme:AppThemeVariants.HighContrastLight}" contains "Light"
            // too, and matching that one would file the high contrast dictionary under the light theme.
            var name = ThemeNames
                .OrderByDescending(candidate => candidate.Length)
                .FirstOrDefault(candidate => key.Contains(candidate, StringComparison.Ordinal))
                ?? key;
            themes[name] = dictionary.Elements()
                .Where(brush => brush.Attribute("Color") is not null)
                .ToDictionary(
                    brush => brush.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value,
                    brush => brush.Attribute("Color")!.Value,
                    StringComparer.Ordinal);
        }

        return themes;
    }

    private static Dictionary<string, string> LoadScalars()
    {
        var document = XDocument.Load(GetTokenPath());
        return document.Descendants()
            .Where(element => element.Name.LocalName is not "ResourceDictionary" and not "SolidColorBrush")
            .Where(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"))
            .GroupBy(
                element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value.Trim(),
                StringComparer.Ordinal);
    }

    private static string GetTokenPath() => System.IO.Path.Combine(
        RepositoryLayout.Root,
        "src",
        "ApSolutions.LocalMedia.Presentation",
        "Theme",
        "DesignTokens.axaml");

    private static double ParseThickness(string value) =>
        double.Parse(value.Split(',')[0], CultureInfo.InvariantCulture);

    private static void AssertContrastAtLeast(
        Dictionary<string, string> brushes,
        string foregroundKey,
        string backgroundKey,
        double minimum,
        string scenario)
    {
        var ratio = Contrast(ParseRgb(brushes[foregroundKey]), ParseRgb(brushes[backgroundKey]));
        Assert.True(ratio >= minimum, $"{scenario} contrast was {ratio:F2}:1; expected at least {minimum:F1}:1.");
    }

    private static (byte Red, byte Green, byte Blue) ParseRgb(string value)
    {
        Assert.StartsWith("#", value, StringComparison.Ordinal);
        var hex = value[1..];
        Assert.True(
            hex.Length is 6,
            $"'{value}' is not an opaque colour, so a contrast ratio would be a guess at what it "
                + "is drawn over. Translucent tokens are decorative and are not measured here.");
        return (
            byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double Contrast((byte Red, byte Green, byte Blue) first, (byte Red, byte Green, byte Blue) second)
    {
        var lighter = Math.Max(Luminance(first), Luminance(second));
        var darker = Math.Min(Luminance(first), Luminance(second));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance((byte Red, byte Green, byte Blue) color)
    {
        static double Linearize(byte channel)
        {
            var value = channel / 255.0;
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Linearize(color.Red))
            + (0.7152 * Linearize(color.Green))
            + (0.0722 * Linearize(color.Blue));
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }
}
