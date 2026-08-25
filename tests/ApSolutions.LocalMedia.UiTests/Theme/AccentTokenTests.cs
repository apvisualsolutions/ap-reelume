// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Everything the accent decides has to actually follow it when somebody changes it.
/// </summary>
/// <remarks>
/// <para>
/// The Appearance page lets a person pick the accent, and the first version of that changed four
/// tokens and almost nothing on screen — the owner reported it as "ni slider ni checks ni nada".
/// The cause is a shape this repository already knows: Fluent's controls read their own resource
/// names, and the token file points those at the accent with a <b>static</b> reference, which
/// resolves once when the dictionary loads and never looks again.
/// </para>
/// <para>
/// So the service writes every redirection as well, and this reads the token file to make sure it
/// writes <b>all</b> of them. A redirection added later and forgotten here would be a control that
/// silently keeps the accent it was built with, which is exactly the defect this exists to prevent —
/// and it cannot be caught by looking at the screen, because nineteen of the twenty do follow.
/// </para>
/// </remarks>
public sealed class AccentTokenTests
{
    [Fact]
    public void Every_token_that_redirects_to_the_accent_is_written_when_the_accent_changes()
    {
        var declared = Redirections();

        // Anti-blindness floor: if the parser stops finding redirections the gate would pass by
        // measuring nothing. There are twenty right now, across six control types.
        Assert.True(
            declared.Count >= 15,
            $"only {declared.Count} accent redirections were found in the token file, so this gate "
                + "is reading the wrong thing rather than finding a tidy theme.");

        var written = AppearanceService.AccentResources;
        var missing = declared.Where(key => !written.Contains(key, StringComparer.Ordinal)).ToArray();
        Assert.True(
            missing.Length == 0,
            $"{string.Join(", ", missing)} — declared in the theme as a redirection to the accent and "
                + "not written by AppearanceService, so a control reading it keeps whichever accent "
                + "the dictionary was loaded with.");

        // And the four the redirections point at are written too, or the redirections would be the
        // only thing that moved.
        foreach (var token in new[] { "AccentBrush", "AccentSubtleBrush", "AccentInkBrush", "AccentTextBrush" })
        {
            Assert.Contains(token, written, StringComparer.Ordinal);
        }
    }

    [Fact]
    public void Nothing_is_written_that_the_theme_does_not_declare()
    {
        // The other direction, and it matters: a key written here and named nowhere in the token
        // file is a resource this application invents, which no control reads and no gate measures.
        var declared = Redirections();
        var tokens = new[] { "AccentBrush", "AccentSubtleBrush", "AccentInkBrush", "AccentTextBrush" };
        var unexpected = AppearanceService.AccentResources
            .Where(key => !declared.Contains(key, StringComparer.Ordinal))
            .Where(key => !tokens.Contains(key, StringComparer.Ordinal))
            .ToArray();

        Assert.True(
            unexpected.Length == 0,
            $"{string.Join(", ", unexpected)} — written by AppearanceService and declared by no "
                + "redirection in the token file.");
    }

    private static List<string> Redirections()
    {
        var path = Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Theme",
            "DesignTokens.axaml");
        var text = File.ReadAllText(path);
        var matches = Regex.Matches(
            text,
            """<StaticResource\s+x:Key="(?<key>[^"]+)"\s+ResourceKey="Accent(?<tone>[A-Za-z]*)Brush"\s*/>""",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        return [.. matches.Select(match => match.Groups["key"].Value).Distinct(StringComparer.Ordinal)];
    }
}
