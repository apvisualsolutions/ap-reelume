// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// WIN-003: the main window opened at the same place and size on every start, whatever the person
/// had dragged it to. The placement now survives the close the way the playback position does.
/// </summary>
public sealed class MainWindowPlacementTests
{
    [AvaloniaFact]
    public void The_place_a_window_was_closed_at_is_where_the_next_one_opens()
    {
        var store = new InMemoryStore();

        var first = new Window { Width = 1180, Height = 760 };
        new MainWindowPlacement(store).Attach(first);
        first.Show();
        Dispatcher.UIThread.RunJobs();
        first.Position = new PixelPoint(210, 140);
        first.Width = 1024;
        first.Height = 640;
        Dispatcher.UIThread.RunJobs();
        first.Close();
        Dispatcher.UIThread.RunJobs();

        var stored = store.Read<StoredWindowPlacement>(MainWindowPlacement.SettingKey);
        Assert.NotNull(stored);
        Assert.Equal(210, stored!.X);
        Assert.Equal(140, stored.Y);
        Assert.Equal(1024, stored.Width);
        Assert.Equal(640, stored.Height);
        Assert.False(stored.IsMaximized);

        var second = new Window { Width = 1180, Height = 760 };
        new MainWindowPlacement(store).Attach(second);
        second.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new PixelPoint(210, 140), second.Position);
        Assert.Equal(1024, second.Width);
        Assert.Equal(640, second.Height);
        second.Close();
    }

    /// <summary>
    /// A window closed maximized reopens maximized — but over the bounds it would restore to, so
    /// un-maximizing later does not land on a screen-sized rectangle.
    /// </summary>
    [AvaloniaFact]
    public void A_window_closed_maximized_reopens_maximized_over_its_normal_bounds()
    {
        var store = new InMemoryStore();
        var first = new Window { Width = 1180, Height = 760 };
        new MainWindowPlacement(store).Attach(first);
        first.Show();
        Dispatcher.UIThread.RunJobs();
        first.Position = new PixelPoint(80, 60);
        first.Width = 1000;
        first.Height = 700;
        Dispatcher.UIThread.RunJobs();
        first.WindowState = WindowState.Maximized;
        Dispatcher.UIThread.RunJobs();
        first.Close();
        Dispatcher.UIThread.RunJobs();

        var stored = store.Read<StoredWindowPlacement>(MainWindowPlacement.SettingKey);
        Assert.NotNull(stored);
        Assert.True(stored!.IsMaximized);
        Assert.Equal(80, stored.X);
        Assert.Equal(1000, stored.Width);

        var second = new Window { Width = 1180, Height = 760 };
        new MainWindowPlacement(store).Attach(second);
        second.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(WindowState.Maximized, second.WindowState);
        second.Close();
    }

    /// <summary>
    /// A position on a monitor that is no longer there is discarded rather than restored: opening
    /// off every screen is the one outcome worse than forgetting.
    /// </summary>
    [AvaloniaFact]
    public void A_position_no_screen_shows_is_discarded()
    {
        var store = new InMemoryStore();
        store.Write(
            MainWindowPlacement.SettingKey,
            new StoredWindowPlacement(-30_000, -30_000, 1024, 640, IsMaximized: false));

        var window = new Window { Width = 1180, Height = 760 };
        new MainWindowPlacement(store).Attach(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(new PixelPoint(-30_000, -30_000), window.Position);
        Assert.Equal(1180, window.Width);
        window.Close();
    }

    private sealed class InMemoryStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }
}
