// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The playback mode reaches the shell's own window, and not only its view model.
/// </summary>
/// <remarks>
/// <b>It reached nothing until 2026-09-02</b>, and the owner reported it as «aun no funciona la
/// pantalla completa». <c>ApplyPlaybackMode</c> set two flags — which arrow the transport draws —
/// and then built a window only for the mini player. <c>PlayerWindowCoordinator</c> had the
/// fullscreen geometry, tested and reachable, and <b>nobody ever called it with that mode</b>. So
/// pressing fullscreen swapped a glyph and left the window where it was.
/// <para>
/// Nothing went red for it because the suite that owns this asserted on <c>shell.PlaybackMode</c>:
/// the view model did change, every time. What was missing was the half nobody was asking about —
/// whether anything on screen followed. That is this file.
/// </para>
/// </remarks>
public sealed class ShellWindowModeTests
{
    private static readonly TitleId Title = new(new Guid("11111111-1111-1111-1111-111111111111"));
    private static readonly MediaFileId MediaFile = new(Title.Value);

    [AvaloniaFact]
    public async Task Asking_for_fullscreen_puts_the_shells_own_window_into_that_state()
    {
        var shell = new ShellViewModel(new NavigationService(), ShellAssemblyTests.EditorSurfaces());
        var window = new Window { Width = 900, Height = 700, Content = new ShellView { DataContext = shell } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowState.Normal, window.WindowState);

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PlaybackMode.Fullscreen, shell.PlaybackMode);
        Assert.Equal(WindowState.FullScreen, window.WindowState);

        window.Close();
    }

    /// <summary>
    /// Leaving fullscreen gives back the window that went in, not the coordinator's default.
    /// </summary>
    /// <remarks>
    /// The half that is easier to lose: a window left in the state has its Width and Height stored
    /// and never drawn, so the way out has to drop the state before it writes the geometry. And the
    /// geometry it writes is the one remembered on the way in — a person who had the window at 900
    /// wide gets 900 back, not the 1180 the coordinator falls back to.
    /// </remarks>
    [AvaloniaFact]
    public async Task Leaving_fullscreen_gives_back_the_window_that_went_in()
    {
        var shell = new ShellViewModel(new NavigationService(), ShellAssemblyTests.EditorSurfaces());
        var window = new Window { Width = 900, Height = 700, Content = new ShellView { DataContext = shell } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await shell.OpenPlayerAsync(
            new PlayDetailsRequest(MediaFile, TimeSpan.Zero),
            TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowState.FullScreen, window.WindowState);

        await shell.TogglePlaybackModeAsync(PlaybackMode.Fullscreen, TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PlaybackMode.Embedded, shell.PlaybackMode);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.Equal(900, window.Width);
        Assert.Equal(700, window.Height);

        window.Close();
    }
}
