using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// Windows text scaling up to 200 % must not cost the person any words. A label that is neither
/// wrapped nor trimmed and no longer fits has silently lost its text.
/// </summary>
public sealed class TextScalingTests
{
    private static readonly double[] Scales = [1.0, 1.5, 2.0];
    private static readonly string[] Languages = ["es-ES", "en-US"];

    [AvaloniaFact]
    public void No_label_loses_its_words_between_one_hundred_and_two_hundred_percent()
    {
        var audit = new AuditLog(nameof(No_label_loses_its_words_between_one_hundred_and_two_hundred_percent));

        foreach (var language in Languages)
        {
            foreach (var scale in Scales)
            {
                foreach (var surface in CanonicalJourney.Surfaces)
                {
                    using var host = CanonicalJourney.Show(surface, language, scale: scale);
                    foreach (var text in host.View.GetVisualDescendants().OfType<TextBlock>())
                    {
                        if (!text.IsEffectivelyVisible || string.IsNullOrWhiteSpace(text.Text))
                        {
                            continue;
                        }

                        if (text.TextWrapping != TextWrapping.NoWrap
                            || text.TextTrimming != TextTrimming.None
                            || text.Bounds.Width <= 0)
                        {
                            continue;
                        }

                        // DesiredSize carries the margin, Bounds does not; comparing them raw reports
                        // every margined label as clipped.
                        var needed = text.DesiredSize.Width - text.Margin.Left - text.Margin.Right;
                        if (needed <= text.Bounds.Width + 0.5)
                        {
                            continue;
                        }

                        audit.Add(
                            surface.StepId,
                            surface.Surface,
                            Shorten(text.Text),
                            DefectSeverity.Major,
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"At {scale * 100:F0} % the label needs {needed:F0} px but "
                                    + $"only has {text.Bounds.Width:F0} px, and it neither wraps nor trims."),
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"Set Windows text scaling to {scale * 100:F0} %, open {surface.Surface} "
                                    + $"in {language} and read \"{Shorten(text.Text)}\"."));
                    }
                }
            }
        }

        audit.Complete();
    }

    [AvaloniaFact]
    public void No_surface_pushes_content_off_the_viewport_at_two_hundred_percent()
    {
        var audit = new AuditLog(nameof(No_surface_pushes_content_off_the_viewport_at_two_hundred_percent));

        foreach (var surface in CanonicalJourney.Surfaces)
        {
            using var host = CanonicalJourney.Show(surface, scale: 2.0);
            var viewport = host.Window.Width;
            var overflow = host.View.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0)
                .Where(control => control is Button or TextBox or ComboBox or CheckBox)
                .Where(control => control.TemplatedParent is null)
                .Select(control => new
                {
                    Control = control,
                    Right = (control.TranslatePoint(new Point(0, 0), host.Window) ?? default).X
                        + control.Bounds.Width,
                })
                .Where(entry => entry.Right > viewport + 0.5)
                .ToArray();

            foreach (var entry in overflow)
            {
                audit.Add(
                    surface.StepId,
                    surface.Surface,
                    entry.Control.Name
                        ?? AutomationProperties.GetName(entry.Control)
                        ?? entry.Control.GetType().Name,
                    DefectSeverity.Major,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"At 200 % the control ends at {entry.Right:F0} px, past the {viewport:F0} px viewport, so it cannot be reached with a mouse."),
                    $"Set Windows text scaling to 200 % and open {surface.Surface}.");
            }
        }

        audit.Complete();
    }

    private static string Shorten(string text) =>
        text.Length <= 40 ? text : text[..37] + "...";
}
