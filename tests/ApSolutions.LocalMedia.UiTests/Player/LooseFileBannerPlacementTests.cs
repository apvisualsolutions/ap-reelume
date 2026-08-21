// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Where the loose-file banner sits: above the picture rather than on top of it, and outside the
/// surface that travels to the mini player.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for a top band "not overlaid on the video" and, in the same row, for 48 px of height.
/// <b>Half of that is right and half of it was written blind</b> — the row marks itself "blocked: the
/// defect measured on 17-08 stops it reaching the screen, so I cannot verify it". Measured on
/// 2026-08-21: this banner carries a heading, the file's name, a wrapping explanation, its action, and
/// a confirmation panel with an explanation and two buttons of its own; it asks for <b>660x286</b> in a
/// 1280 window. Forty-eight would keep the heading and drop everything that makes the notice mean
/// anything, so the height is refused and the placement is done.
/// </para>
/// <para>
/// The placement matters twice over. <c>PlayerStage</c> is not just the picture: it is the control the
/// shell hands to the mini player window and takes back. A banner inside it <b>travels</b> — and
/// measured alone at the mini player's 480, it asks for <b>336 px of height in a window 270 tall</b>.
/// </para>
/// <para>
/// And it must not sit inside <c>PlayerHost</c> either, which is the trap this repository has walked
/// into before: coming back from mini mode the shell runs <c>host.Content = stage</c>, so anything
/// declared beside the stage inside that host is replaced by the stage and never comes back. A tree
/// declared in markup that something else replaces on arrival is the defect of the house.
/// </para>
/// </remarks>
public sealed class LooseFileBannerPlacementTests
{
    /// <summary>
    /// The banner is outside the surface that travels, and outside the host that gets reassigned.
    /// </summary>
    [AvaloniaFact]
    public void The_banner_is_neither_on_the_stage_nor_inside_the_host_that_is_reassigned()
    {
        var (window, shell) = Show();

        var stage = shell.FindControl<Panel>("PlayerStage");
        Assert.NotNull(stage);
        var host = shell.FindControl<ContentControl>("PlayerHost");
        Assert.NotNull(host);
        var banner = Assert.Single(shell.GetVisualDescendants().OfType<LooseFileBanner>());

        Assert.DoesNotContain(banner, stage!.GetVisualDescendants());
        Assert.DoesNotContain(banner, host!.GetVisualDescendants());
        window.Close();
    }

    /// <summary>
    /// It ends where the picture starts, rather than being drawn over it.
    /// </summary>
    /// <remarks>
    /// Geometry and not structure, because the two say different things: a banner could be a sibling
    /// of the stage and still be painted across it. What is asserted is that its bottom edge is at or
    /// above the stage's top edge in the shell's own coordinates.
    /// </remarks>
    [AvaloniaFact]
    public void The_banner_ends_where_the_picture_starts()
    {
        var (window, shell) = Show();

        var stage = shell.FindControl<Panel>("PlayerStage")!;
        var banner = Assert.Single(shell.GetVisualDescendants().OfType<LooseFileBanner>());

        var bannerBottom = banner.TranslatePoint(new Point(0, banner.Bounds.Height), shell);
        var stageTop = stage.TranslatePoint(new Point(0, 0), shell);
        Assert.True(bannerBottom is not null && stageTop is not null);
        Assert.True(
            bannerBottom!.Value.Y <= stageTop!.Value.Y,
            $"the banner ends at y={bannerBottom.Value.Y:F0} and the picture starts at "
                + $"y={stageTop.Value.Y:F0}, so it is drawn over the film.");

        Assert.True(banner.Bounds.Height > 0, "the banner measured nothing, so this proves nothing.");
        window.Close();
    }

    /// <summary>
    /// The shell is shown without a view model, which is what puts every branch on screen at once.
    /// </summary>
    /// <remarks>
    /// A binding that resolves to nothing leaves <c>IsVisible</c> at its default, so the banner is
    /// present here without a loose session having to be arranged — and the player surface is visible
    /// for the same reason. That makes this the widest the layout ever is, which is the case worth
    /// measuring.
    /// </remarks>
    private static (Window Window, ShellView Shell) Show()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var shell = new ShellView();
        var window = new Window { Width = 1280, Height = 800, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, shell);
    }
}
