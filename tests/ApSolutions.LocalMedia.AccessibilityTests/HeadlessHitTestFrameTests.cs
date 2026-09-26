// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// That a headless hit test answers from the last rendered frame and not from the visual tree, which
/// is the mechanism behind `ENG-026` and the reason the walk renders a frame before it chooses where
/// to click.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it cost.</b> The assembled walk failed five times on runs that changed no code, the last
/// three in <c>The_players_transport_is_operated_with_the_mouse</c> with one message: the point beside
/// Pause was chosen with the home view under it and clicked on the player. The harness decides that a
/// point is safe — not on a picture, which pauses since `ENG-018` — by asking <c>InputHitTest</c>, and
/// that answer came from a frame drawn before the player appeared. The input helpers render a frame
/// themselves, so the click that followed saw the player, landed on the film and paused it.
/// </para>
/// <para>
/// <b>Three findings, measured ten times each in this suite's own headless configuration</b>, which
/// draws nothing: after a full layout pass the hit test still names the element that used to be
/// there; <c>ForceRenderTimerTick</c> — the obvious fix, and the first one written — changed that in
/// <b>0 of 10</b>, because it asks for a tick rather than delivering one; and
/// <c>CaptureRenderedFrame</c>, like a mouse move, brought the hit test up to date in <b>10 of 10</b>.
/// The first fix would have shipped green here and kept failing in CI.
/// </para>
/// <para>
/// <b>Only the half that works is asserted, and that is measured too.</b> A first version also
/// asserted the stale half — that after layout the hit test still names the old element — and it
/// failed inside the full suite: there the real render timer sometimes ticks on its own during the
/// test, and the hit test comes back current. A test that depends on the timer not ticking is the
/// kind of intermittent red this file exists to end, so the stale half lives in
/// <c>docs/evidence/stable/ENG026-hit-test-stale-frame.md</c> with its numbers, and what is asserted
/// here is what the walk relies on: after a capture, the hit test always names what is on screen.
/// </para>
/// </remarks>
public sealed class HeadlessHitTestFrameTests
{
    private static readonly Point Middle = new(200, 150);

    [AvaloniaFact]
    public void Capturing_the_rendered_frame_brings_the_hit_test_up_to_date()
    {
        var (window, over) = ShowCoveredLater();

        Reveal(window, over);
        _ = window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Over", NameAt(window));
        window.Close();
    }

    /// <summary>A window showing one element, with a second ready to cover it.</summary>
    private static (Window Window, Border Over) ShowCoveredLater()
    {
        var under = new Border { Name = "Under", Background = Brushes.Red };
        var over = new Border { Name = "Over", Background = Brushes.Blue, IsVisible = false };
        var window = new Window { Width = 400, Height = 300, Content = new Panel { Children = { under, over } } };
        window.Show();
        _ = window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Under", NameAt(window));
        return (window, over);
    }

    /// <summary>Shows the cover and lays it out the way the walk's Reveal does.</summary>
    private static void Reveal(Window window, Border over)
    {
        over.IsVisible = true;
        window.InvalidateMeasure();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static string? NameAt(Window window) => (window.InputHitTest(Middle) as Control)?.Name;
}
