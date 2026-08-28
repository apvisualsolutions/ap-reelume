// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The always-on-top mini player. It hosts the same player surface the embedded view uses; the
/// coordinator moves that control here rather than creating a second one.
/// </summary>
/// <remarks>
/// <para>
/// Hosting the surface in a panel of its own, rather than as the window's whole content, is what
/// lets the window keep a tree of its own. Until 2026-08-19 it did not: the coordinator assigned
/// <c>Window.Content</c>, and the five controls this window needs would have been discarded the
/// moment a session arrived — declared, never shown, and never pressed.
/// </para>
/// <para>
/// Since 2026-08-28 it has no frame, and everything a frame used to give it is given back here: the
/// window moves on a press anywhere its own controls are not, it resizes from any of its eight
/// edges, and a resize keeps the picture at 16:9. The two decisions — <see cref="ResizeEdgeAt"/> and
/// <see cref="ShouldBeginMoveDrag"/> — are static and take no window on purpose. What they answer is
/// the whole of the behaviour, and the platform calls beside them are one line each that a headless
/// test cannot observe.
/// </para>
/// </remarks>
public sealed partial class MiniPlayerWindow : Window
{
    /// <summary>How far from an edge a press still counts as a grab of that edge, in logical units.</summary>
    /// <remarks>
    /// Not a design token and deliberately not in the markup: it is the size of a target for a
    /// pointer, which is the one number the scales in <c>DesignTokens.axaml</c> do not describe.
    /// Eight matches what Windows itself gives a sizing border at 100%.
    /// </remarks>
    public const double ResizeMargin = 8;

    private bool _isAdjustingForAspect;
    private Size _lastUserSize;

    public MiniPlayerWindow()
    {
        InitializeComponent();
        _lastUserSize = new Size(Width, Height);
    }

    /// <summary>
    /// Which edge a press at this point grabs, or null when the press is not on an edge at all.
    /// </summary>
    /// <remarks>
    /// A point outside the window answers null rather than the nearest edge: a pointer that has left
    /// the window is not grabbing anything, and the arithmetic below would otherwise call every
    /// negative coordinate a grab of the west edge.
    /// </remarks>
    public static WindowEdge? ResizeEdgeAt(Point point, Size size, double margin)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(margin);
        if (point.X < 0 || point.Y < 0 || point.X > size.Width || point.Y > size.Height)
        {
            return null;
        }

        var west = point.X <= margin;
        var east = point.X >= size.Width - margin;
        var north = point.Y <= margin;
        var south = point.Y >= size.Height - margin;

        // A window narrower than two margins would report a point as both west and east. West wins,
        // because the arithmetic has to answer one edge and the alternative is a null that leaves a
        // small window with no way to be resized at all.
        return (west, east, north, south) switch
        {
            (true, _, true, _) => WindowEdge.NorthWest,
            (true, _, _, true) => WindowEdge.SouthWest,
            (_, true, true, _) => WindowEdge.NorthEast,
            (_, true, _, true) => WindowEdge.SouthEast,
            (true, _, _, _) => WindowEdge.West,
            (_, true, _, _) => WindowEdge.East,
            (_, _, true, _) => WindowEdge.North,
            (_, _, _, true) => WindowEdge.South,
            _ => null,
        };
    }

    /// <summary>Whether a press at this height lands on the chrome rather than on the picture.</summary>
    /// <remarks>
    /// The one pixel of border under the chrome is counted as chrome, which costs nothing: it is
    /// inside the south resize margin, and an edge is answered before this is asked.
    /// </remarks>
    public static bool IsOverChrome(Point point, double windowHeight, double chromeHeight) =>
        point.Y >= windowHeight - chromeHeight;

    /// <summary>
    /// Whether a press should move the window rather than reach whatever is under it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three conditions, and the third was measured rather than reasoned. The first draft skipped a
    /// press that something had already handled, on the assumption that a button marks its own: it
    /// does not. A press on <c>MiniPlayerPlayPause</c> arrives at this window unhandled — Avalonia's
    /// button marks the <em>release</em>, which is where its click is — so that guard protected
    /// nothing and every one of the five controls would have dragged the window instead of working.
    /// </para>
    /// <para>
    /// So what is asked is where the press is and not what it hit: the picture drags the window, and
    /// the strip the chrome sits in does not. The edge check comes first for the same reason in the
    /// other direction — without it the eight sizing borders would each move the window, which is a
    /// mini player that cannot be resized at all.
    /// </para>
    /// </remarks>
    public static bool ShouldBeginMoveDrag(bool isLeftButton, WindowEdge? edge, bool isOverChrome) =>
        isLeftButton && edge is null && !isOverChrome;

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

    /// <summary>Where this window is and how big it is, in the units the coordinator works in.</summary>
    public PlayerWindowGeometry CurrentGeometry() => new(
        Position.X / RenderScaling,
        Position.Y / RenderScaling,
        Width,
        Height);

    /// <summary>
    /// Puts the window back on the shape of the picture after somebody has resized it.
    /// </summary>
    /// <remarks>
    /// Public so a test can ask for it without a platform that resizes windows: the headless backend
    /// raises no <c>User</c> resize, so the reason filter below would never let one through and the
    /// whole of the ratio would go unmeasured.
    /// </remarks>
    public void ApplyVideoAspect()
    {
        var requested = CurrentGeometry();
        var constrained = PlayerWindowCoordinator.ConstrainToVideoAspect(
            requested,
            requested with { Width = _lastUserSize.Width, Height = _lastUserSize.Height },
            PlayerWindowCoordinator.MiniVideoAspect,
            MiniPlayerChrome.Bounds.Height,
            MinWidth);

        _isAdjustingForAspect = true;
        try
        {
            Width = constrained.Width;
            Height = constrained.Height;
        }
        finally
        {
            _isAdjustingForAspect = false;
        }

        _lastUserSize = new Size(constrained.Width, constrained.Height);
    }

    /// <summary>
    /// Answers a resize: keeps the shape when a person dragged it, records the baseline otherwise.
    /// </summary>
    /// <remarks>
    /// Public and taking the reason rather than the event, because the reason is the one thing a
    /// headless backend never says: measured on 2026-08-28, every resize it raises arrives as
    /// <c>Layout</c>, so a filter buried in the override would leave the whole correction untested
    /// behind a branch nothing could take.
    /// </remarks>
    public void HandleResize(WindowResizeReason reason)
    {
        // The correction answering itself is dropped first, and that is what keeps the ratio from
        // oscillating: assigning Width raises this again, and the assignment after it once more.
        if (_isAdjustingForAspect)
        {
            return;
        }

        if (reason == WindowResizeReason.User)
        {
            ApplyVideoAspect();
            return;
        }

        // A size this window was given rather than dragged to — the coordinator applying a recalled
        // placement is the one that happens — still becomes the baseline. Leaving it behind would
        // make the next drag measure its movement against a size two placements ago and give way on
        // the wrong axis.
        _lastUserSize = new Size(Width, Height);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        var edge = ResizeEdgeAt(point.Position, new Size(Width, Height), ResizeMargin);
        if (edge is { } grabbed)
        {
            BeginResizeDrag(grabbed, e);
            return;
        }

        if (ShouldBeginMoveDrag(
            point.Properties.IsLeftButtonPressed,
            edge,
            IsOverChrome(point.Position, Height, MiniPlayerChrome.Bounds.Height)))
        {
            BeginMoveDrag(e);
        }
    }

    /// <inheritdoc />
    protected override void OnResized(WindowResizedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnResized(e);
        HandleResize(e.Reason);
    }
}
