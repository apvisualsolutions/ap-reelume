// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The mini player's own five controls.
/// </summary>
/// <remarks>
/// <para>
/// They live in a view of their own rather than in <c>MiniPlayerWindow.axaml</c> for a measured
/// reason: the autonomous walk records what it pressed against the nearest <c>UserControl</c> above
/// the control, and a button declared straight inside a <c>Window</c> sits under none. It would be a
/// control the walk could never write down, which in this repository is a control nobody presses
/// before somebody installs it.
/// </para>
/// <para>
/// Its data context is the shell's, which already holds the session, the mode and the close: the
/// mini player needs no view model of its own, and a second one would be a second answer to
/// questions that already have one.
/// </para>
/// </remarks>
public sealed partial class MiniPlayerChromeView : UserControl
{
    public MiniPlayerChromeView()
    {
        InitializeComponent();
    }
}
