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
/// Puts a window into the shape a playback mode asks for.
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
    /// <summary>The shape of the picture, which is the shape the prototype's mini player keeps.</summary>
    /// <remarks>
    /// The prototype draws the mini player as a fixed panel whose picture carries
    /// <c>aspect-ratio:16/9</c> and whose controls sit under it. So the ratio belongs to the video and
    /// not to the window: the chrome's height is added on top of it, which is what
    /// <see cref="ConstrainToVideoAspect"/> takes as its last argument.
    /// </remarks>
    public const double MiniVideoAspect = 16.0 / 9.0;

    private readonly Dictionary<PlaybackMode, PlayerWindowGeometry> _geometry = [];
    private readonly IMiniPlayerPlacementStore? _placements;

    public PlayerWindowCoordinator()
        : this(placements: null)
    {
    }

    /// <summary>
    /// Builds a coordinator that also remembers the mini player between sessions.
    /// </summary>
    /// <remarks>
    /// Optional, and every other constructor in this file went without it until 2026-08-28, because
    /// the tests and the recovery shell both build a coordinator with nothing behind it. What the
    /// store adds is only the half that outlives the process: within one run the dictionary above
    /// still answers first, so a mini player moved twice does not go to disk to learn where it is.
    /// </remarks>
    public PlayerWindowCoordinator(IMiniPlayerPlacementStore? placements) => _placements = placements;

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

    /// <summary>
    /// Keeps a resize on the shape of the picture, deriving whichever side moved less.
    /// </summary>
    /// <remarks>
    /// Which side gives way is decided by which one the pointer moved further, because a mini player
    /// that always derived its height would never answer a drag on its bottom edge — the window would
    /// snap back on every frame and read as broken rather than as constrained. Both sides are
    /// clamped to the minimum before the ratio is applied, so a window squeezed against its own floor
    /// still comes out of here in shape.
    /// </remarks>
    public static PlayerWindowGeometry ConstrainToVideoAspect(
        PlayerWindowGeometry requested,
        PlayerWindowGeometry previous,
        double aspect,
        double chromeHeight,
        double minimumWidth)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(aspect, 0);
        ArgumentOutOfRangeException.ThrowIfNegative(chromeHeight);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumWidth, 0);

        var widthMoved = Math.Abs(requested.Width - previous.Width);
        var heightMoved = Math.Abs(requested.Height - previous.Height);
        var width = widthMoved >= heightMoved
            ? requested.Width
            : (requested.Height - chromeHeight) * aspect;
        width = Math.Max(minimumWidth, width);

        return requested with { Width = width, Height = (width / aspect) + chromeHeight };
    }

    /// <summary>Remembers the geometry of a mode, but only while it is actually on screen.</summary>
    /// <remarks>
    /// The mini player's is also written through to the placement store, when there is one: that is
    /// the only path by which a window moved in one session opens where it was left in the next.
    /// Until 2026-08-28 nothing at all called this method — it was remembered and recalled by its own
    /// tests and by nobody else, which is this repository's characteristic defect wearing the shape
    /// of a coordinator.
    /// </remarks>
    public void Remember(
        PlaybackMode mode,
        PlayerWindowGeometry geometry,
        PixelRect screenBounds,
        double scaling)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (!geometry.IsVisibleOn(screenBounds, scaling))
        {
            return;
        }

        _geometry[mode] = geometry;
        if (mode == PlaybackMode.Mini)
        {
            _placements?.Save(new MiniPlayerPlacement(
                geometry.X,
                geometry.Y,
                geometry.Width,
                geometry.Height));
        }
    }

    /// <summary>The stored geometry for a mode, or null when none is visible.</summary>
    /// <remarks>
    /// This session's answer wins over the stored one. They only differ on the first switch after a
    /// launch, and on that one the stored answer is the whole point.
    /// </remarks>
    public PlayerWindowGeometry? Recall(PlaybackMode mode)
    {
        if (_geometry.TryGetValue(mode, out var geometry))
        {
            return geometry;
        }

        if (mode != PlaybackMode.Mini || _placements?.Read() is not { } stored)
        {
            return null;
        }

        return new PlayerWindowGeometry(stored.X, stored.Y, stored.Width, stored.Height);
    }

    /// <summary>
    /// Applies a mode to a window: where it sits, how big it is, whether it stays on top, and
    /// whether it keeps its decorations.
    /// </summary>
    /// <remarks>
    /// It does not move the surface, and that is the whole of the change made on 2026-08-19. It used
    /// to assign <c>Window.Content</c>, which replaces everything the window declared for itself —
    /// the mini player's own five controls included. Moving the surface belongs to whoever owns the
    /// window and knows where inside it the picture goes; this knows only the shape.
    /// </remarks>
    public void Apply(Window window, PlaybackMode mode, PixelRect screenBounds, double scaling)
    {
        ArgumentNullException.ThrowIfNull(window);

        // Recalled and then checked against this screen, and the two are not the same test. What
        // Remember refused was a placement off the screen it was written on; a placement written on
        // a second monitor is perfectly valid there and lands on nothing here, so the window would
        // open where nobody could reach it — with no title bar to drag it back by.
        var recalled = Recall(mode);
        var geometry = recalled is not null && recalled.IsVisibleOn(screenBounds, scaling)
            ? recalled
            : GeometryFor(mode, screenBounds, scaling);

        // The mini player loses its frame too, and it is the only mode where that costs something:
        // fullscreen keeps the shortcuts and the transport, and this one keeps neither a title bar to
        // drag by nor a system close. Both are given back by MiniPlayerWindow — the move on a press
        // anywhere the chrome is not, and the close as one of its own five buttons.
        window.WindowDecorations = mode == PlaybackMode.Embedded
            ? WindowDecorations.Full
            : WindowDecorations.None;
        window.Topmost = mode == PlaybackMode.Mini;
        window.Position = new PixelPoint(
            (int)Math.Round(geometry.X * scaling),
            (int)Math.Round(geometry.Y * scaling));
        window.Width = geometry.Width;
        window.Height = geometry.Height;
        Current = mode;
    }
}
