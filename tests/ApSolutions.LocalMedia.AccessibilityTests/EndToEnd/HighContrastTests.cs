// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;
using ApSolutions.LocalMedia.TestSupport;
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

    /// <summary>
    /// Where a bound colour is the <b>subject</b> rather than a state, named one at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule this list excepts is a good one and is not relaxed: a colour standing in for a state
    /// leaves anybody who cannot see that colour with nothing. What a swatch does is different in kind
    /// — the colour is not standing for something else, it <b>is</b> the thing being chosen, and the
    /// value itself is on screen beside it in a text box somebody can read and edit.
    /// </para>
    /// <para>
    /// An entry is a view and the exact binding it is allowed, so a second bound colour in the same
    /// view still fails. The list may only shrink, and the count is asserted so that a typo which
    /// stopped matching would not quietly except everything.
    /// </para>
    /// </remarks>
    private static readonly (string View, string Property, string Source)[] ColourIsTheSubject =
    [
        ("SubtitleStyleView", "Foreground", "ForegroundHex"),
        ("SubtitleStyleView", "Background", "BackgroundHex"),
    ];

    /// <summary>
    /// Colour that repeats what is already written, rather than standing in for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A second list rather than two more rows in the one above, because the reason is a different
    /// one and merging them would let either reason excuse either case. There the colour <b>is</b>
    /// the thing being chosen; here it says which title this is — and the title is written under the
    /// card, its initials are on top of it, and the accessible name of the button around it is that
    /// same title. Somebody who cannot see the colour loses nothing, because nothing is only there.
    /// </para>
    /// <para>
    /// <b>And the exception is paid for.</b> A view on this list has to switch its computed colour
    /// off in the two modes where colour is not allowed to mean anything, which is asserted below
    /// rather than promised here: the layer carries <c>PosterArtOpacity</c>, and that token is 0 in
    /// both high contrasts. Without that clause this list would be the loophole the other one is
    /// careful not to be. It may only shrink, and its length is asserted for the same reason.
    /// </para>
    /// </remarks>
    private static readonly (string View, string Property, string Source)[] ColourRepeatsWhatIsWritten =
    [
        ("PosterCardView", "Background", "Title"),

        // The hero's bleed is the same coin, paid the same way: the colour says which title this
        // is, the title is written beside it at display size, and the layer switches off through
        // PosterArtOpacity in both high contrasts — asserted below like the card's.
        ("ResumeHeroView", "Background", "ResumeTitle"),
    ];

    [AvaloniaFact]
    public void No_state_is_told_by_colour_alone()
    {
        var audit = new AuditLog(nameof(No_state_is_told_by_colour_alone));
        Assert.Equal(2, ColourIsTheSubject.Length);
        Assert.Equal(2, ColourRepeatsWhatIsWritten.Length);

        foreach (var view in ViewFiles())
        {
            var name = Path.GetFileNameWithoutExtension(view);
            var document = XDocument.Load(view);
            var colourBoundToState = document.Descendants()
                .Attributes()
                .Where(attribute => BrushAttributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                .Where(attribute => attribute.Value.Contains("{Binding", StringComparison.Ordinal))
                .Where(attribute => !ColourIsTheSubject.Any(allowed =>
                    allowed.View == name
                    && allowed.Property == attribute.Name.LocalName
                    && attribute.Value.Contains(allowed.Source, StringComparison.Ordinal)))
                .Where(attribute => !ColourRepeatsWhatIsWritten.Any(allowed =>
                    allowed.View == name
                    && allowed.Property == attribute.Name.LocalName
                    && attribute.Value.Contains(allowed.Source, StringComparison.Ordinal)))
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

    /// <summary>
    /// A view excused for repeating what is written switches that colour off in high contrast.
    /// </summary>
    /// <remarks>
    /// This is the clause that makes the second exception list safe to have. It is measured in two
    /// halves, because either alone would pass while the other was broken: the view multiplies the
    /// computed layer by <c>PosterArtOpacity</c>, and that token is 0 in both high contrast
    /// dictionaries. A view that stopped reading the token, or a token that stopped being 0, is a
    /// colour that started meaning something in the mode where it must not.
    /// </remarks>
    [AvaloniaFact]
    public void A_colour_that_repeats_what_is_written_is_switched_off_in_high_contrast()
    {
        foreach (var (view, _, _) in ColourRepeatsWhatIsWritten)
        {
            var file = ViewFiles().Single(
                candidate => Path.GetFileNameWithoutExtension(candidate) == view);
            Assert.Contains(
                "Opacity=\"{DynamicResource PosterArtOpacity}\"",
                File.ReadAllText(file),
                StringComparison.Ordinal);
        }

        var tokens = XDocument.Load(Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Theme",
            "DesignTokens.axaml"));
        var declared = tokens.Descendants()
            .Where(element => element.Name.LocalName == "Double")
            .Where(element => element.Attributes()
                .Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "PosterArtOpacity"))
            .Select(element => element.Value.Trim())
            .ToArray();

        Assert.Equal(4, declared.Length);
        Assert.Equal(["0", "0", "1", "1"], declared.Order(StringComparer.Ordinal));
    }

    private static bool IsRendered(Control control) =>
        control.IsEffectivelyVisible && control.Bounds is { Width: > 0, Height: > 0 };

    private static IReadOnlyList<string> ViewFiles() =>
        [.. Directory.EnumerateFiles(
            Path.Combine(RepositoryLayout.Root, "src", "ApSolutions.LocalMedia.Presentation"),
            "*.axaml",
            SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.Combine("Theme", "DesignTokens"), StringComparison.Ordinal)
                && !path.Contains(Path.Combine("Resources", "Brand"), StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)];

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6,8}$")]
    private static partial Regex LiteralColour();
}
