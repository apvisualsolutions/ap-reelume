// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The mini player as a picture-in-picture window: no frame, moved by dragging it, resized from any
/// edge, and kept on the shape of the picture while it is.
/// </summary>
/// <remarks>
/// What is asserted here is the decisions, not the platform calls beside them. <c>BeginMoveDrag</c>
/// and <c>BeginResizeDrag</c> hand the gesture to the window manager and answer nothing a headless
/// backend could be asked about; a test that called them would prove the line exists, which the
/// compiler already did. So the two questions the window asks first — which edge is under the
/// pointer, and whether this press moves the window — are static, and they are the whole of the
/// behaviour that could be wrong.
/// </remarks>
public sealed class MiniPlayerWindowTests
{
    private static readonly Size Mini480X270 = new(480, 270);

    [Theory]
    [InlineData(4, 4, WindowEdge.NorthWest)]
    [InlineData(476, 4, WindowEdge.NorthEast)]
    [InlineData(4, 266, WindowEdge.SouthWest)]
    [InlineData(476, 266, WindowEdge.SouthEast)]
    [InlineData(240, 2, WindowEdge.North)]
    [InlineData(240, 268, WindowEdge.South)]
    [InlineData(2, 135, WindowEdge.West)]
    [InlineData(478, 135, WindowEdge.East)]
    public void Every_edge_of_a_window_with_no_frame_can_be_grabbed(double x, double y, WindowEdge expected)
    {
        Assert.Equal(
            expected,
            MiniPlayerWindow.ResizeEdgeAt(new Point(x, y), Mini480X270, MiniPlayerWindow.ResizeMargin));
    }

    [Theory]
    [InlineData(240, 135)]
    [InlineData(100, 60)]
    public void The_middle_of_the_window_grabs_no_edge_and_therefore_moves_it(double x, double y)
    {
        var edge = MiniPlayerWindow.ResizeEdgeAt(
            new Point(x, y),
            Mini480X270,
            MiniPlayerWindow.ResizeMargin);

        Assert.Null(edge);
        Assert.True(MiniPlayerWindow.ShouldBeginMoveDrag(
            isLeftButton: true,
            edge,
            isOverChrome: false));
    }

    /// <summary>
    /// A press on an edge resizes and never moves, and a press on the chrome does neither.
    /// </summary>
    /// <remarks>
    /// The chrome half is the one that was measured rather than assumed, and it went the other way:
    /// a button in Avalonia does not mark a press as handled — it marks the release, where its click
    /// is — so a window that dragged on any unhandled press would drag on all five of the mini
    /// player's own controls. What decides is where the press is, and the strip the chrome sits in
    /// is not the picture.
    /// </remarks>
    [Fact]
    public void Only_a_left_press_on_the_picture_moves_the_window()
    {
        Assert.False(MiniPlayerWindow.ShouldBeginMoveDrag(true, WindowEdge.SouthEast, false));
        Assert.False(MiniPlayerWindow.ShouldBeginMoveDrag(false, edge: null, false));
        Assert.False(MiniPlayerWindow.ShouldBeginMoveDrag(false, WindowEdge.North, false));
        Assert.False(MiniPlayerWindow.ShouldBeginMoveDrag(true, edge: null, isOverChrome: true));
        Assert.True(MiniPlayerWindow.ShouldBeginMoveDrag(true, edge: null, isOverChrome: false));
    }

    [Theory]
    [InlineData(135, false)]
    [InlineData(225, false)]
    [InlineData(226, true)]
    [InlineData(269, true)]
    public void The_strip_the_chrome_sits_in_is_not_the_picture(double y, bool expected)
    {
        Assert.Equal(
            expected,
            MiniPlayerWindow.IsOverChrome(new Point(240, y), windowHeight: 270, chromeHeight: 44));
    }

    [Theory]
    [InlineData(-1, 135)]
    [InlineData(240, -1)]
    [InlineData(481, 135)]
    [InlineData(240, 271)]
    public void A_pointer_that_has_left_the_window_grabs_nothing(double x, double y)
    {
        Assert.Null(MiniPlayerWindow.ResizeEdgeAt(
            new Point(x, y),
            Mini480X270,
            MiniPlayerWindow.ResizeMargin));
    }

    [Fact]
    public void A_negative_margin_is_refused_rather_than_grabbing_nothing_anywhere()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MiniPlayerWindow.ResizeEdgeAt(new Point(0, 0), Mini480X270, -1));
    }

    /// <summary>
    /// A window narrower than two margins still answers one edge rather than both or neither.
    /// </summary>
    /// <remarks>
    /// The window's own minimum is 320 so this cannot happen through the frame, but the margin is a
    /// number and the size is a number: the arithmetic has to have an answer, and a null here would
    /// be a small window that cannot be resized.
    /// </remarks>
    [Fact]
    public void A_window_narrower_than_two_margins_still_answers_a_single_edge()
    {
        var edge = MiniPlayerWindow.ResizeEdgeAt(new Point(5, 5), new Size(10, 10), margin: 8);

        Assert.Equal(WindowEdge.NorthWest, edge);
    }

    [AvaloniaFact]
    public void The_mini_player_opens_without_a_frame_and_says_where_it_ends()
    {
        var mini = new MiniPlayerWindow();
        mini.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(WindowDecorations.None, mini.WindowDecorations);

        // Without decorations the border is the only thing between the picture and the desktop, so
        // it is asserted rather than assumed: a thickness of zero would leave a window with no edge
        // at all, which is also where the eight resize margins are.
        var frame = Assert.IsType<Border>(mini.Content);
        Assert.Equal(1, frame.BorderThickness.Top);
        Assert.NotNull(frame.BorderBrush);
        mini.Close();
    }

    /// <summary>
    /// A resize comes back on the shape of the picture, and the chrome's height is added to it.
    /// </summary>
    /// <remarks>
    /// Driven through <c>ApplyVideoAspect</c> rather than by resizing the window, because the
    /// headless backend raises no user resize and the handler's reason filter would drop it. What is
    /// under test is the correction; that it is reached on a real drag is the one line the filter is.
    /// </remarks>
    [AvaloniaFact]
    public void A_resize_comes_back_on_sixteen_by_nine_with_the_chrome_on_top()
    {
        var mini = new MiniPlayerWindow { DataContext = null };
        mini.Show();
        Dispatcher.UIThread.RunJobs();
        var chrome = mini.GetControl<MiniPlayerChromeView>("MiniPlayerChrome");

        mini.Width = 640;
        Dispatcher.UIThread.RunJobs();
        mini.ApplyVideoAspect();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(640, mini.Width);
        Assert.Equal(
            (640 / PlayerWindowCoordinator.MiniVideoAspect) + chrome.Bounds.Height,
            mini.Height,
            precision: 6);
        mini.Close();
    }

    /// <summary>
    /// A resize the window was given rather than dragged to leaves the picture alone.
    /// </summary>
    /// <remarks>
    /// Every resize a headless backend raises arrives as <c>Layout</c> — measured on 2026-08-28 —
    /// which is exactly the case this asserts: the coordinator applying a recalled placement must not
    /// have its own width answered with a height of the window's choosing.
    /// </remarks>
    [AvaloniaFact]
    public void A_size_the_window_was_given_is_kept_and_only_a_drag_is_corrected()
    {
        var mini = new MiniPlayerWindow();
        mini.Show();
        Dispatcher.UIThread.RunJobs();

        mini.Width = 600;
        mini.Height = 500;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(600, mini.Width);
        Assert.Equal(500, mini.Height);

        // And the same size arriving as a drag is corrected, from the baseline the assignment above
        // left behind: the width moved further, so the height is the one that gives way.
        mini.Width = 720;
        Dispatcher.UIThread.RunJobs();
        mini.HandleResize(WindowResizeReason.User);
        Dispatcher.UIThread.RunJobs();

        var chrome = mini.GetControl<MiniPlayerChromeView>("MiniPlayerChrome");
        Assert.Equal(720, mini.Width);
        Assert.Equal(
            (720 / PlayerWindowCoordinator.MiniVideoAspect) + chrome.Bounds.Height,
            mini.Height,
            precision: 6);
        mini.Close();
    }

    /// <summary>
    /// The gesture itself, on the three places a press can land.
    /// </summary>
    /// <remarks>
    /// <c>BeginMoveDrag</c> and <c>BeginResizeDrag</c> hand the gesture to a window manager that a
    /// headless run does not have, so what is asserted is that the window survives all three and
    /// keeps its size: the decisions above say which one is taken, and this says the path to them is
    /// wired and does not throw. The press on the chrome is the one that would have been a defect —
    /// on this path the window used to start dragging under the play button.
    /// </remarks>
    [AvaloniaFact]
    public void A_press_on_the_picture_the_edge_and_the_chrome_all_leave_the_window_standing()
    {
        var mini = new MiniPlayerWindow();
        mini.Show();
        Dispatcher.UIThread.RunJobs();
        var chrome = mini.GetControl<MiniPlayerChromeView>("MiniPlayerChrome");
        var play = chrome.GetControl<Button>("MiniPlayerPlayPause");
        var onThePlayButton = play.TranslatePoint(
            new Point(play.Bounds.Width / 2, play.Bounds.Height / 2),
            mini);
        Assert.NotNull(onThePlayButton);
        Assert.True(MiniPlayerWindow.IsOverChrome(
            onThePlayButton!.Value,
            mini.Height,
            chrome.Bounds.Height));

        foreach (var point in new[]
        {
            new Point(240, 100),
            new Point(mini.Width - 2, mini.Height - 2),
            onThePlayButton!.Value,
        })
        {
            mini.MouseDown(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            mini.MouseUp(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Equal(480, mini.Width);
        Assert.Equal(270, mini.Height);
        mini.Close();
    }

    /// <summary>Where the window is, in the units the coordinator remembers it in.</summary>
    [AvaloniaFact]
    public void The_window_reports_its_own_geometry_in_logical_units()
    {
        var mini = new MiniPlayerWindow();
        mini.Show();
        mini.Position = new PixelPoint(300, 200);
        Dispatcher.UIThread.RunJobs();

        var geometry = mini.CurrentGeometry();

        Assert.Equal(mini.Position.X / mini.RenderScaling, geometry.X);
        Assert.Equal(mini.Position.Y / mini.RenderScaling, geometry.Y);
        Assert.Equal(mini.Width, geometry.Width);
        Assert.Equal(mini.Height, geometry.Height);
        mini.Close();
    }
}
