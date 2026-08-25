// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Every row of buttons that carries translated words wraps instead of running off the side.
/// </summary>
/// <remarks>
/// <para>
/// A horizontal <c>StackPanel</c> holding buttons whose labels are translated is the shape that has
/// drawn a control off the side of the window <b>seven times</b> in this repository. §4 asks for
/// <c>WrapPanel</c> by name wherever it appears, and this is the table of the ones that have been
/// decided, so a row added later cannot quietly be a <c>StackPanel</c> again.
/// </para>
/// <para>
/// It reads the markup rather than a mounted screen on purpose: <c>ViewOverflowTests</c> already
/// measures width, but it measures it in one language at one font scale, and a row that fits in
/// Spanish today is not a row that wraps. What is asserted here is the panel that <b>can</b> wrap,
/// which is the property that survives a longer translation.
/// </para>
/// </remarks>
public sealed class WrappingSurfaceTests
{
    /// <summary>The surfaces decided so far, by the view that declares them.</summary>
    private static readonly (string View, string Surface)[] Wrapping =
    [
        ("Catalog/TitleActionsView.axaml", "TitleActionsSurface"),
        ("Library/LibraryView.axaml", "LibraryFilterSurface"),
        ("Movie/MovieDetailsView.axaml", "MovieActionsSurface"),
        ("Show/ShowDetailsView.axaml", "ShowActionsSurface"),
        ("Player/PlayerView.axaml", "RecoveryActionsSurface"),
    ];

    [Fact]
    public void Every_decided_row_of_actions_is_a_wrap_panel()
    {
        var wrong = new List<string>();

        foreach (var (view, surface) in Wrapping)
        {
            var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
                $"src/ApSolutions.LocalMedia.Presentation/{view}"));
            var declaration = Regex.Match(
                markup,
                $@"<(?<panel>\w+)\s[^>]*x:Name=""{surface}""",
                RegexOptions.None,
                TimeSpan.FromSeconds(2));

            if (!declaration.Success)
            {
                wrong.Add($"{surface} is not declared in {view} any more.");
                continue;
            }

            if (declaration.Groups["panel"].Value != "WrapPanel")
            {
                wrong.Add($"{surface} is a {declaration.Groups["panel"].Value}, not a WrapPanel.");
            }
        }

        Assert.Empty(wrong);

        // A floor against blindness: a regex that stopped matching would find nothing and agree with
        // an empty list of problems.
        Assert.Equal(5, Wrapping.Length);
    }
}
