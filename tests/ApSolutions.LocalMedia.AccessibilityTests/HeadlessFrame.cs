// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// Brings a headless window's layout AND its last rendered frame up to date, which is what a hit test
/// reads. It lives here, and not inside the walk, so that the walk and
/// <see cref="HeadlessHitTestFrameTests"/> run the very same lines: until 2026-09-27 the test carried its
/// own copy of them, and removing the capture from the walk left every test green.
/// </summary>
internal static class HeadlessFrame
{
    /// <summary>
    /// A full layout pass, then a rendered frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>InvalidateMeasure and not UpdateLayout alone.</b> UpdateLayout runs the pass only if the
    /// tree is dirty, and measured over a full walk on 2026-09-02 the window reported itself valid on
    /// all 250 beside-clicks while forcing a pass moved a control in five of them.
    /// </para>
    /// <para>
    /// <b>And a rendered frame after it, because a hit test does not read the tree</b>: it answers
    /// from the last rendered frame, measured on 2026-09-26. <b>CaptureRenderedFrame and not
    /// ForceRenderTimerTick</b>: in this suite's configuration the tick left the hit test stale in
    /// 10 of 10 tries, and the capture brought it up to date in 10 of 10.
    /// </para>
    /// </remarks>
    public static void SettleAndDraw(TopLevel window)
    {
        window.InvalidateMeasure();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        _ = window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }
}
