// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.Presentation.Updates;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Updates;

/// <summary>
/// What the redesign asks of the update surface: one primary action, and corners from the token
/// rather than from a number written twice.
/// </summary>
/// <remarks>
/// The view is mounted without a data context on purpose. Neither question depends on one — a class
/// is a class and a corner radius is a corner radius — and a view model would only add a way for this
/// to fail for a reason that is not what it is asking about.
/// </remarks>
public sealed class UpdateViewDesignTests
{
    /// <summary>The one action this screen is for.</summary>
    private static readonly string[] LeadingAction = ["UpdateCheckButton"];

    /// <summary>The two surfaces the redesign gives a corner to.</summary>
    private static readonly string[] BorderedSurfaces = ["UpdateStatusSurface", "UpdateOfferSurface"];

    /// <summary>
    /// Check is the point of this screen, and it is the only one that can be.
    /// </summary>
    /// <remarks>
    /// Download and install appear <em>by state</em> — they are not on screen until there is an offer
    /// and until it has been fetched — and cancel is never the thing a screen is for. So the primary
    /// action is asserted as the only one, not merely as present: two primary actions is a screen that
    /// has not decided what it is for, and it would pass an assertion that only looked for one.
    /// </remarks>
    [AvaloniaFact]
    public void Exactly_one_button_leads_the_update_screen()
    {
        var (window, view) = Show();

        var leading = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("primary-action"))
            .Select(button => button.Name ?? "<unnamed>")
            .ToArray();

        Assert.Equal(LeadingAction, leading);
        window.Close();
    }

    /// <summary>
    /// Both bordered surfaces take their corner from the theme, and the markup says so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The source is what is asserted, not only the painted value, and that distinction is the whole
    /// test. <c>CornerRadiusMedium</c> is 8 and the literals here were 8, so a comparison of painted
    /// numbers passed <b>before</b> the view was changed at all — it would have gone green on a view
    /// that spends no token and drifts the day the theme moves. Reading the markup is what tells a
    /// number that agrees from a number that is the same one.
    /// </para>
    /// <para>
    /// The painted value is asserted too, because markup that names a resource proves nothing about
    /// what reached the screen, and the token is resolved rather than written down: a copy of 8 in
    /// here would agree with itself while the theme said something else.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_bordered_surfaces_take_their_corner_from_the_theme()
    {
        var (window, view) = Show();
        var expected = Assert.IsType<CornerRadius>(
            Avalonia.Application.Current!.TryFindResource("CornerRadiusMedium", out var token)
                ? token
                : null);
        Assert.True(
            expected.TopLeft > 0,
            "CornerRadiusMedium resolved to nothing, so comparing against it proves nothing.");

        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Updates/UpdateView.axaml"));
        var literals = Regex.Matches(markup, @"CornerRadius=""[0-9]", RegexOptions.None, TimeSpan.FromSeconds(2));
        Assert.True(
            literals.Count == 0,
            $"UpdateView.axaml still writes {literals.Count} corner radius as a number. A number "
                + "written beside a token is a number that will disagree with it.");

        foreach (var name in BorderedSurfaces)
        {
            var border = view.GetVisualDescendants()
                .OfType<Border>()
                .SingleOrDefault(candidate => candidate.Name == name);

            Assert.True(border is not null, $"{name} is not on the update surface.");
            Assert.Equal(expected, border!.CornerRadius);
        }

        window.Close();
    }

    private static (Window Window, UpdateView View) Show()
    {
        var view = new UpdateView();
        var window = new Window { Width = 900, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }
}
