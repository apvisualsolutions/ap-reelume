using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

public sealed class ContrastTokenTests
{
    [Fact]
    public void Text_control_and_focus_tokens_meet_WCAG_AA_across_four_visual_modes()
    {
        var resources = LoadTokens();
        var modes = new[]
        {
            (Name: "Light", Background: "LightSurfaceColor", Text: "LightTextPrimaryColor", Fill: "LightControlFillColor", Border: "LightControlBorderColor", Focus: "LightFocusColor"),
            (Name: "Dark", Background: "DarkSurfaceColor", Text: "DarkTextPrimaryColor", Fill: "DarkControlFillColor", Border: "DarkControlBorderColor", Focus: "DarkFocusColor"),
            (Name: "HighContrastLight", Background: "HighContrastLightSurfaceColor", Text: "HighContrastLightTextColor", Fill: "HighContrastLightControlFillColor", Border: "HighContrastLightControlBorderColor", Focus: "HighContrastLightFocusColor"),
            (Name: "HighContrastDark", Background: "HighContrastDarkSurfaceColor", Text: "HighContrastDarkTextColor", Fill: "HighContrastDarkControlFillColor", Border: "HighContrastDarkControlBorderColor", Focus: "HighContrastDarkFocusColor"),
        };

        foreach (var mode in modes)
        {
            AssertContrastAtLeast(resources, mode.Text, mode.Background, 4.5, $"{mode.Name} text");
            AssertContrastAtLeast(resources, mode.Border, mode.Fill, 3.0, $"{mode.Name} control boundary");
            AssertContrastAtLeast(resources, mode.Focus, mode.Background, 3.0, $"{mode.Name} focus");
        }
    }

    [Fact]
    public void Focus_is_at_least_two_pixels_and_selected_state_has_a_non_color_cue()
    {
        var resources = LoadTokens();
        var thickness = double.Parse(resources["FocusStrokeThickness"], CultureInfo.InvariantCulture);

        Assert.True(thickness >= 2.0, $"Focus thickness was {thickness}.");
        Assert.False(string.IsNullOrWhiteSpace(resources["SelectedStateGlyph"]));

        var document = XDocument.Load(GetTokenPath());
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "Setter"),
            setter => setter.Attribute("Property")?.Value == "BorderThickness"
                && setter.Attribute("Value")?.Value.Contains("FocusStrokeThickness", StringComparison.Ordinal) is true);

        var appearancePath = System.IO.Path.Combine(
            GetRepositoryRoot(),
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
    public void Reduced_motion_token_is_zero_while_standard_motion_remains_bounded()
    {
        var resources = LoadTokens();
        var reduced = double.Parse(resources["MotionDurationReducedMilliseconds"], CultureInfo.InvariantCulture);
        var standard = double.Parse(resources["MotionDurationStandardMilliseconds"], CultureInfo.InvariantCulture);

        Assert.Equal(0, reduced);
        Assert.InRange(standard, 1, 250);
    }

    [Fact]
    public void Mica_and_Windows_motion_detection_are_isolated_to_the_Windows_host()
    {
        var repositoryRoot = GetRepositoryRoot();
        var presentationRoot = System.IO.Path.Combine(
            repositoryRoot,
            "src",
            "ApSolutions.LocalMedia.Presentation");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories),
            file => File.ReadAllText(file).Contains("MicaBackdropService", StringComparison.Ordinal));

        var presentation = Assembly.Load("ApSolutions.LocalMedia.Presentation");
        var windows = Assembly.Load("ApSolutions.LocalMedia.Windows");
        var backdropContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IBackdropService");
        var reducedMotionContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IReducedMotionService");
        var mica = RequireType(
            windows,
            "ApSolutions.LocalMedia.Windows.Windowing.MicaBackdropService");
        var reducedMotion = RequireType(
            windows,
            "ApSolutions.LocalMedia.Windows.Accessibility.WindowsReducedMotionService");

        Assert.True(backdropContract.IsAssignableFrom(mica));
        Assert.True(reducedMotionContract.IsAssignableFrom(reducedMotion));
        Assert.Equal("ShellSurfaceBrush", mica.GetProperty("SolidFallbackResourceKey")?.GetValue(null));
    }

    private static Dictionary<string, string> LoadTokens()
    {
        var path = GetTokenPath();
        Assert.True(File.Exists(path), $"Design token dictionary is missing: {path}");
        var document = XDocument.Load(path);
        return document.Descendants()
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
        GetRepositoryRoot(),
        "src",
        "ApSolutions.LocalMedia.Presentation",
        "Theme",
        "DesignTokens.axaml");

    private static void AssertContrastAtLeast(
        Dictionary<string, string> resources,
        string foregroundKey,
        string backgroundKey,
        double minimum,
        string scenario)
    {
        var ratio = Contrast(ParseRgb(resources[foregroundKey]), ParseRgb(resources[backgroundKey]));
        Assert.True(ratio >= minimum, $"{scenario} contrast was {ratio:F2}:1; expected at least {minimum:F1}:1.");
    }

    private static (byte Red, byte Green, byte Blue) ParseRgb(string value)
    {
        Assert.StartsWith("#", value, StringComparison.Ordinal);
        var hex = value[1..];
        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        Assert.Equal(6, hex.Length);
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

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
