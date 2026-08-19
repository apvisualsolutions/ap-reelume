// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The always-on-top mini player. It hosts the same player surface the embedded view uses; the
/// coordinator moves that control here rather than creating a second one.
/// </summary>
/// <remarks>
/// Hosting the surface in a panel of its own, rather than as the window's whole content, is what
/// lets the window keep a tree of its own. Until 2026-08-19 it did not: the coordinator assigned
/// <c>Window.Content</c>, and the five controls this window needs would have been discarded the
/// moment a session arrived — declared, never shown, and never pressed.
/// </remarks>
public sealed partial class MiniPlayerWindow : Window
{
    public MiniPlayerWindow()
    {
        InitializeComponent();
    }

    /// <summary>Places the shared player surface inside this window, beside the chrome.</summary>
    public void Host(Control surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        // Already here: re-parenting a surface that has not moved would tear the video frame down
        // and build it again, and a mode applied twice is a session lost for no reason.
        if (MiniPlayerSurface.Children.Count == 1
            && ReferenceEquals(MiniPlayerSurface.Children[0], surface))
        {
            return;
        }

        if (surface.Parent is ContentControl previous)
        {
            previous.Content = null;
        }

        MiniPlayerSurface.Children.Clear();
        MiniPlayerSurface.Children.Add(surface);
    }

    /// <summary>
    /// Lets go of the surface so somewhere else can take it.
    /// </summary>
    /// <remarks>
    /// The shell calls this before handing the stage back to its embedded host: a control that still
    /// has a visual parent cannot be given a second one, and the symptom would be a mode change that
    /// throws rather than a picture that moves.
    /// </remarks>
    public void Release() => MiniPlayerSurface.Children.Clear();
}
