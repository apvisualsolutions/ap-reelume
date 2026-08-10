// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using Avalonia;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>The window geometry a mode uses, in logical units.</summary>
public sealed record PlayerWindowGeometry(double X, double Y, double Width, double Height)
{
    public bool IsVisibleOn(PixelRect screenBounds, double scaling)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scaling, 0);
        var logical = new Rect(
            screenBounds.X / scaling,
            screenBounds.Y / scaling,
            screenBounds.Width / scaling,
            screenBounds.Height / scaling);
        return Width > 0
            && Height > 0
            && X + Width > logical.X
            && Y + Height > logical.Y
            && X < logical.Right
            && Y < logical.Bottom;
    }
}

/// <summary>
/// Moves the same player surface between embedded, fullscreen, and mini without recreating it.
/// </summary>
/// <remarks>
/// Fullscreen deliberately does not use <c>WindowState.FullScreen</c>. On a scaled display that
/// state delivers a client size in physical pixels while rendering still applies the scale factor,
/// so everything lands at the scale factor times its intended position and a bottom-anchored bar
/// falls off the screen. Sizing the window to the screen bounds divided by the scaling keeps layout
/// and rendering in the same units, which is what this coordinator does.
/// </remarks>
public sealed class PlayerWindowCoordinator
{
    private readonly Dictionary<PlaybackMode, PlayerWindowGeometry> _geometry = [];

    /// <summary>The mode the surface is presented in right now.</summary>
    public PlaybackMode Current { get; private set; } = PlaybackMode.Embedded;

    /// <summary>Mini player geometry when nothing has been stored yet, in logical units.</summary>
    public static PlayerWindowGeometry DefaultMiniGeometry { get; } = new(24, 24, 480, 270);

    /// <summary>
    /// Computes the window geometry for a mode. Fullscreen uses the screen the window is on,
    /// converted from physical pixels into the logical units the layout works in.
    /// </summary>
    public static PlayerWindowGeometry GeometryFor(
        PlaybackMode mode,
        PixelRect screenBounds,
        double scaling,
        PlayerWindowGeometry? embedded = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scaling, 0);
        return mode switch
        {
            PlaybackMode.Fullscreen => new PlayerWindowGeometry(
                screenBounds.X / scaling,
                screenBounds.Y / scaling,
                screenBounds.Width / scaling,
                screenBounds.Height / scaling),
            PlaybackMode.Mini => DefaultMiniGeometry,
            _ => embedded ?? new PlayerWindowGeometry(0, 0, 1180, 760),
        };
    }

    /// <summary>Remembers the geometry of a mode, but only while it is actually on screen.</summary>
    public void Remember(
        PlaybackMode mode,
        PlayerWindowGeometry geometry,
        PixelRect screenBounds,
        double scaling)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (geometry.IsVisibleOn(screenBounds, scaling))
        {
            _geometry[mode] = geometry;
        }
    }

    /// <summary>The stored geometry for a mode, or null when none is visible.</summary>
    public PlayerWindowGeometry? Recall(PlaybackMode mode) =>
        _geometry.TryGetValue(mode, out var geometry) ? geometry : null;

    /// <summary>
    /// Applies a mode to a window and moves the player content into it. The content instance is the
    /// same object throughout, which is what keeps the session and its surface alive.
    /// </summary>
    public void Apply(Window window, Control content, PlaybackMode mode, PixelRect screenBounds, double scaling)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(content);
        var geometry = Recall(mode) ?? GeometryFor(mode, screenBounds, scaling);

        if (!ReferenceEquals(window.Content, content))
        {
            if (content.Parent is ContentControl previousHost)
            {
                previousHost.Content = null;
            }

            window.Content = content;
        }

        window.WindowDecorations = mode == PlaybackMode.Fullscreen
            ? WindowDecorations.None
            : WindowDecorations.Full;
        window.Topmost = mode == PlaybackMode.Mini;
        window.Position = new PixelPoint(
            (int)Math.Round(geometry.X * scaling),
            (int)Math.Round(geometry.Y * scaling));
        window.Width = geometry.Width;
        window.Height = geometry.Height;
        Current = mode;
    }
}
