// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// In fullscreen the picture owns the screen, which is a different question from the window owning it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The owner reported «aun no funciona la pantalla completa» twice, on 2026-08-25 and again on
/// 2026-09-13, and the cause was different each time.</b> The first was that nothing moved the window;
/// <see cref="ShellWindowModeTests"/> was written for it and closes it. The second is this: the window
/// does go fullscreen, and <b>the picture does not fill it</b>, because the navigation rail and the
/// header come back the moment the pointer moves — and they take layout space.
/// </para>
/// <para>
/// <b>Why the existing suite could not see it.</b> Every fullscreen assertion in this tree is about
/// <c>shell.PlaybackMode</c> or <c>window.WindowState</c>: the model, every time. Nothing asked how
/// wide the picture ended up. It is this repository's own named defect — measuring the model is not
/// measuring the ink — and the file that was written to close the first report says so in its own
/// remark while still only asserting the window.
/// </para>
/// <para>
/// The rule being enforced is already decided in <c>ADR-0010</c>: a state takes space and an event
/// floats. In fullscreen the shell's chrome is an event, so it must not take space from the picture.
/// </para>
/// </remarks>
public sealed class FullscreenPictureTests
{
    private static readonly TitleId Title = new(new Guid("22222222-2222-2222-2222-222222222222"));
    private static readonly MediaFileId MediaFile = new(Title.Value);

    private const double WindowWidth = 1280;
    private const double WindowHeight = 720;

    /// <summary>
    /// With the chrome revealed in fullscreen, the picture is still as wide as the window.
    /// </summary>
    /// <remarks>
    /// Revealing is what a pointer move does, and a pointer move is unavoidable: the gesture that
    /// asks for fullscreen is a double click. So this is the state a person is actually in one instant
    /// after asking, and it is the one nothing was measuring.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_revealed_chrome_takes_no_width_from_the_picture_in_fullscreen()
    {
        var (shell, window, surface) = await OpenFullscreen();
        try
        {
            // What moving the mouse does, and it never un-does itself while a film plays.
            shell.RevealChrome();
            Dispatcher.UIThread.RunJobs();

            // Against the window as it is NOW, never against the size it was created with. The
            // coordinator resizes the window on the way into fullscreen, so comparing with the
            // constant let a framed picture pass by growing the thing it was measured against —
            // this file's first draft did exactly that and called the defect fixed.
            var available = window.Bounds.Width;
            Assert.True(
                available > 0,
                "The window measured zero wide, so there is nothing to compare the picture against.");
            Assert.True(
                surface.Bounds.Width >= available - 1,
                $"In fullscreen the picture is {surface.Bounds.Width} wide inside a window of "
                    + $"{available}, so the chrome is taking space from it. In fullscreen the chrome "
                    + "is an event and floats (ADR-0010); it does not stand beside the picture.");
        }
        finally
        {
            await Leave(shell, window);
        }
    }

    /// <summary>
    /// And no height either, which is the half a rail on the left would not have shown.
    /// </summary>
    [AvaloniaFact]
    public async Task The_revealed_chrome_takes_no_height_from_the_picture_in_fullscreen()
    {
        var (shell, window, surface) = await OpenFullscreen();
        try
        {
            shell.RevealChrome();
            Dispatcher.UIThread.RunJobs();

            var available = window.Bounds.Height;
            Assert.True(
                available > 0,
                "The window measured zero tall, so there is nothing to compare the picture against.");
            Assert.True(
                surface.Bounds.Height >= available - 1,
                $"In fullscreen the picture is {surface.Bounds.Height} tall inside a window of "
                    + $"{available}, so the header is taking space from it.");
        }
        finally
        {
            await Leave(shell, window);
        }
    }

    /// <summary>
    /// The instrument's floor: outside fullscreen the chrome DOES take space, on purpose.
    /// </summary>
    /// <remarks>
    /// Without this the two tests above would pass on a shell that had no chrome at all, or on a
    /// measurement that was reading something other than the picture. Embedded playback is a panel
    /// beside a rail and a header, and that is the whole point of the distinction being tested.
    /// </remarks>
    [AvaloniaFact]
    public async Task Outside_fullscreen_the_revealed_chrome_does_take_space_from_the_picture()
    {
        var shell = new ShellViewModel(new NavigationService(), ShellAssemblyTests.EditorSurfaces());
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = WindowWidth, Height = WindowHeight, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        shell.RevealChrome();
        Dispatcher.UIThread.RunJobs();

        var surface = Surface(view);
        try
        {
            Assert.True(
                surface.Bounds.Width > 0,
                "The picture measured zero wide, so the scan is not reading the picture and the two "
                    + "tests above prove nothing.");
            Assert.True(
                surface.Bounds.Width < window.Bounds.Width,
                $"Embedded, the picture is {surface.Bounds.Width} wide in a window of "
                    + $"{window.Bounds.Width}, so the chrome is taking no space anywhere and the "
                    + "fullscreen tests above would pass on a shell that simply has none.");
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<(ShellViewModel Shell, Window Window, VideoFrameView Surface)> OpenFullscreen()
    {
        var shell = new ShellViewModel(new NavigationService(), ShellAssemblyTests.EditorSurfaces());
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = WindowWidth, Height = WindowHeight, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PlaybackMode.Fullscreen, shell.PlaybackMode);

        return (shell, window, Surface(view));
    }

    private static VideoFrameView Surface(ShellView view)
    {
        var surface = view.GetVisualDescendants().OfType<VideoFrameView>().FirstOrDefault();

        Assert.NotNull(surface);

        return surface!;
    }

    /// <summary>
    /// Left before closing, which is not tidiness: a window closed while still in the state leaves it
    /// behind for whatever runs next, and CI answered exactly that on 2026-09-02 with an unrelated
    /// test failing in cleanup.
    /// </summary>
    private static async Task Leave(ShellViewModel shell, Window window)
    {
        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        window.Close();
    }
}
