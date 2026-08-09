using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// High contrast only works when every colour comes from a token the contrast matrix already checked,
/// and when no state is told by colour alone. A literal colour in a view escapes both.
/// </summary>
public sealed partial class HighContrastTests
{
    private static readonly string[] BrushAttributes =
        ["Background", "Foreground", "BorderBrush", "Fill", "Stroke"];

    [AvaloniaFact]
    public void Every_journey_surface_renders_in_both_high_contrast_variants()
    {
        var audit = new AuditLog(nameof(Every_journey_surface_renders_in_both_high_contrast_variants));
        var variants = new[]
        {
            new ThemeVariant("HighContrast", ThemeVariant.Light),
            new ThemeVariant("HighContrast", ThemeVariant.Dark),
        };

        foreach (var variant in variants)
        {
            foreach (var surface in CanonicalJourney.Surfaces)
            {
                using var host = CanonicalJourney.Show(surface, theme: variant);
                var visible = host.View.GetVisualDescendants().OfType<Control>().Count(IsRendered);
                if (visible == 0)
                {
                    audit.Add(
                        surface.StepId,
                        surface.Surface,
                        "surface",
                        DefectSeverity.Critical,
                        $"The surface renders nothing under {variant.Key}.",
                        $"Switch Windows to {variant.Key} and open {surface.Surface}.");
                }
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void No_view_paints_a_colour_of_its_own_outside_the_approved_tokens()
    {
        var audit = new AuditLog(nameof(No_view_paints_a_colour_of_its_own_outside_the_approved_tokens));

        foreach (var view in ViewFiles())
        {
            var document = XDocument.Load(view);
            var literals = document.Descendants()
                .Attributes()
                .Where(attribute => BrushAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                .Where(attribute => LiteralColour().IsMatch(attribute.Value))
                .ToArray();

            foreach (var literal in literals)
            {
                audit.Add(
                    "theme",
                    Path.GetFileNameWithoutExtension(view),
                    $"{literal.Parent?.Name.LocalName}.{literal.Name.LocalName}",
                    DefectSeverity.Major,
                    $"The view paints the literal colour {literal.Value}, which no contrast check covers "
                        + "and high contrast cannot override.",
                    $"Open {Path.GetFileName(view)} and look for {literal.Name.LocalName}=\"{literal.Value}\".");
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void No_state_is_told_by_colour_alone()
    {
        var audit = new AuditLog(nameof(No_state_is_told_by_colour_alone));

        foreach (var view in ViewFiles())
        {
            var document = XDocument.Load(view);
            var colourBoundToState = document.Descendants()
                .Attributes()
                .Where(attribute => BrushAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                .Where(attribute => attribute.Value.Contains("{Binding", StringComparison.Ordinal))
                .ToArray();

            foreach (var binding in colourBoundToState)
            {
                audit.Add(
                    "theme",
                    Path.GetFileNameWithoutExtension(view),
                    $"{binding.Parent?.Name.LocalName}.{binding.Name.LocalName}",
                    DefectSeverity.Major,
                    "A colour is bound straight to view-model state, so that state has no textual "
                        + "counterpart for anyone who cannot see the colour.",
                    $"Open {Path.GetFileName(view)} and look for {binding.Name.LocalName}=\"{binding.Value}\".");
            }
        }

        audit.Complete();
    }

    private static bool IsRendered(Control control) =>
        control.IsEffectivelyVisible && control.Bounds is { Width: > 0, Height: > 0 };

    private static IReadOnlyList<string> ViewFiles() =>
        [.. Directory.EnumerateFiles(
            Path.Combine(AuditLog.GetRepositoryRoot(), "src", "ApSolutions.LocalMedia.Presentation"),
            "*.axaml",
            SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("Theme", "DesignTokens"), StringComparison.Ordinal)
                && !path.Contains(Path.Combine("Resources", "Brand"), StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)];

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6,8}$")]
    private static partial Regex LiteralColour();
}
