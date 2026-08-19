// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>What the walk does next when a press has not shown its effect yet.</summary>
internal enum PressStep
{
    /// <summary>Give the application more time before touching the control again.</summary>
    Wait,

    /// <summary>Press it again, the way a person whose click missed presses again.</summary>
    PressAgain,

    /// <summary>Stop pressing: the control is gone, so let the effect's own wait have the last word.</summary>
    StopPressing,
}

/// <summary>
/// The retry rule of the walk's <c>PressAsync</c>, kept apart from the pressing so it can be measured.
/// </summary>
/// <remarks>
/// A press that changed nothing is repeated, because inside the assembled shell's nested scroll
/// viewers the position a click reaches and the position the layout reports do not always agree.
/// Two things must not be repeated, and both were learned from CI on runners slower than this
/// machine.
///
/// A control that is <b>disabled</b> right now is usually one whose own work is in flight, and that
/// is the application behaving correctly — the transport bar disables a skip while the previous one
/// seeks. Pressing again would land on a disabled control and the harness would report correct
/// behaviour as a failure. So it waits, and when the wait runs out it presses anyway, which keeps a
/// control disabled for some other reason able to say so.
///
/// A control that is no longer <b>visible</b> is a different animal, measured on 2026-08-19: some
/// controls remove themselves by working. Answering the version-switch question closes the question,
/// so <c>RestartSwitchButton</c> is gone the moment it is pressed, while the effect the walk watches
/// — the other version opening — takes longer to arrive on a loaded runner. The old rule only knew
/// about disabled, so it walked straight into pressing a button that had left the screen and reported
/// <c>visible=False, enabled=True</c>: the product doing exactly the right thing, called a failure.
/// Here it waits too, and when the wait runs out it does <b>not</b> press a control that is not
/// there — the effect's own timeout says what actually went wrong.
/// </remarks>
internal static class WalkPressPolicy
{
    /// <summary>How many settle rounds to give a control before pressing on regardless.</summary>
    internal const int WaitLimit = 16;

    /// <summary>What to do with a control whose press has not shown its effect yet.</summary>
    internal static PressStep Next(bool isVisible, bool isEnabled, int waitsSoFar)
    {
        if (isVisible && isEnabled)
        {
            return PressStep.PressAgain;
        }

        if (waitsSoFar < WaitLimit)
        {
            return PressStep.Wait;
        }

        return isVisible ? PressStep.PressAgain : PressStep.StopPressing;
    }
}
