// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The walk's retry rule, which decides whether a press that showed no effect is repeated.
/// </summary>
/// <remarks>
/// It is measured here rather than only through the walk because the case that broke it appears on
/// CI and not on this machine: a control that removes itself by working needs a slow enough runner
/// for the harness to catch it mid-disappearance. A rule that can only be exercised by luck is a rule
/// nobody is checking.
/// </remarks>
public sealed class WalkPressPolicyTests
{
    [Fact]
    public void A_control_that_is_there_and_working_is_pressed_again()
    {
        // The original reason the retry exists: inside nested scroll viewers the same control at the
        // same offset answered a press on one pass and not on the one before it.
        Assert.Equal(PressStep.PressAgain, WalkPressPolicy.Next(isVisible: true, isEnabled: true, waitsSoFar: 0));
        Assert.Equal(
            PressStep.PressAgain,
            WalkPressPolicy.Next(isVisible: true, isEnabled: true, waitsSoFar: WalkPressPolicy.WaitLimit));
    }

    [Fact]
    public void A_disabled_control_is_waited_for_and_then_pressed_anyway()
    {
        // Disabled usually means its own work is in flight, which is the application being correct.
        Assert.Equal(PressStep.Wait, WalkPressPolicy.Next(isVisible: true, isEnabled: false, waitsSoFar: 0));
        Assert.Equal(
            PressStep.Wait,
            WalkPressPolicy.Next(isVisible: true, isEnabled: false, waitsSoFar: WalkPressPolicy.WaitLimit - 1));

        // And then pressed anyway, so a control disabled for a different reason still says so.
        Assert.Equal(
            PressStep.PressAgain,
            WalkPressPolicy.Next(isVisible: true, isEnabled: false, waitsSoFar: WalkPressPolicy.WaitLimit));
    }

    [Fact]
    public void A_control_that_left_the_screen_is_waited_for_and_never_pressed_again()
    {
        // Measured on CI on 2026-08-19: answering the version-switch question closes the question, so
        // RestartSwitchButton is gone the moment it is pressed while the other version is still
        // opening. Pressing it again reported "visible=False, enabled=True" — the product doing the
        // right thing, called a failure.
        Assert.Equal(PressStep.Wait, WalkPressPolicy.Next(isVisible: false, isEnabled: true, waitsSoFar: 0));
        Assert.Equal(
            PressStep.Wait,
            WalkPressPolicy.Next(isVisible: false, isEnabled: true, waitsSoFar: WalkPressPolicy.WaitLimit - 1));

        // And never pressed: a control that is not on screen cannot be pressed by anyone, so the
        // effect's own timeout is what should speak.
        Assert.Equal(
            PressStep.StopPressing,
            WalkPressPolicy.Next(isVisible: false, isEnabled: true, waitsSoFar: WalkPressPolicy.WaitLimit));
        Assert.Equal(
            PressStep.StopPressing,
            WalkPressPolicy.Next(isVisible: false, isEnabled: false, waitsSoFar: WalkPressPolicy.WaitLimit));
    }
}
