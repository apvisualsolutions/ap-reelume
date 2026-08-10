// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Domain.Lifecycle;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.Windows.Tray;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Windows;

/// <summary>
/// What actually happens when the window is closed, in order. Progress reaches storage before
/// anything else, whatever the tray is set to, and an idle tray does nothing at all.
/// </summary>
public sealed class TrayLifecycleTests
{
    [Fact]
    public async Task Closing_without_a_tray_writes_the_progress_and_then_exits()
    {
        var log = new List<string>();
        var close = Build(log, LifecyclePreferences.Default, out var tray);

        var decision = await close.ExecuteAsync(hasActivePlayback: true, TestContext.Current.CancellationToken);

        Assert.Equal(["progress", "stop-playback", "exit"], log);
        Assert.True(decision.ExitApplication);
        Assert.False(tray.IsVisible);
    }

    [Fact]
    public async Task Closing_to_the_tray_writes_the_progress_and_then_hides_the_window()
    {
        var preferences = AppLifecyclePolicy.WithCloseBehavior(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
            CloseBehavior.MinimizeToTray);
        var log = new List<string>();
        var close = Build(log, preferences, out var tray);

        var decision = await close.ExecuteAsync(hasActivePlayback: true, TestContext.Current.CancellationToken);

        Assert.Equal(["progress", "hide-to-tray"], log);
        Assert.False(decision.ExitApplication);
        Assert.True(tray.IsVisible);
    }

    [Fact]
    public async Task An_active_session_keeps_playing_in_the_tray_and_stops_on_a_real_exit()
    {
        var toTray = AppLifecyclePolicy.WithCloseBehavior(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
            CloseBehavior.MinimizeToTray);
        var trayLog = new List<string>();
        await Build(trayLog, toTray, out _)
            .ExecuteAsync(hasActivePlayback: true, TestContext.Current.CancellationToken);
        Assert.DoesNotContain("stop-playback", trayLog);

        var exitLog = new List<string>();
        await Build(exitLog, LifecyclePreferences.Default, out _)
            .ExecuteAsync(hasActivePlayback: true, TestContext.Current.CancellationToken);
        Assert.Contains("stop-playback", exitLog);

        var idleLog = new List<string>();
        await Build(idleLog, LifecyclePreferences.Default, out _)
            .ExecuteAsync(hasActivePlayback: false, TestContext.Current.CancellationToken);
        Assert.Equal(["progress", "exit"], idleLog);
    }

    [Fact]
    public async Task Leaving_through_the_tray_writes_the_progress_before_it_ends_anything()
    {
        var preferences = AppLifecyclePolicy.WithCloseBehavior(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
            CloseBehavior.MinimizeToTray);
        var log = new List<string>();
        var close = Build(log, preferences, out var tray);
        tray.Show();

        var decision = await close.ExitAsync(hasActivePlayback: true, TestContext.Current.CancellationToken);

        Assert.Equal(["progress", "stop-playback", "exit"], log);
        Assert.True(decision.ExitApplication);
        Assert.False(decision.HideToTray);
        Assert.False(tray.IsVisible);
    }

    [Fact]
    public async Task Progress_reaches_storage_before_the_playback_session_is_torn_down()
    {
        var log = new List<string>();
        var close = Build(log, LifecyclePreferences.Default, out _);

        await close.ExecuteAsync(hasActivePlayback: true, TestContext.Current.CancellationToken);

        Assert.True(
            log.IndexOf("progress") < log.IndexOf("stop-playback"),
            "The session was stopped before its position was written.");
    }

    [Fact]
    public async Task An_idle_tray_raises_nothing_and_touches_nothing()
    {
        var tray = new RecordingTrayService();
        tray.Show();

        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        Assert.True(tray.IsVisible);
        Assert.Equal(0, tray.OpenRequests);
        Assert.Equal(0, tray.ExitRequests);
        Assert.Equal(1, tray.ShowCalls);
    }

    [Fact]
    public void Showing_and_hiding_the_tray_twice_is_idempotent()
    {
        var tray = new RecordingTrayService();

        tray.Show();
        tray.Show();
        Assert.True(tray.IsVisible);
        Assert.Equal(1, tray.ShowCalls);

        tray.Hide();
        tray.Hide();
        Assert.False(tray.IsVisible);
        Assert.Equal(1, tray.HideCalls);
    }

    [Fact]
    public async Task The_tray_is_never_shown_for_a_person_who_did_not_ask_for_it()
    {
        var log = new List<string>();
        var close = Build(log, LifecyclePreferences.Default, out var tray);

        await close.ExecuteAsync(hasActivePlayback: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, tray.ShowCalls);
        Assert.False(tray.IsVisible);
    }

    [AvaloniaFact]
    public void The_real_tray_adapter_stays_hidden_until_it_is_shown_and_leaves_nothing_behind()
    {
        using var tray = new WindowsTrayService("AP Reelume", "Open", "Exit");

        Assert.False(tray.IsVisible);

        tray.Show();
        Assert.True(tray.IsVisible);
        tray.Show();
        Assert.True(tray.IsVisible);

        tray.Hide();
        Assert.False(tray.IsVisible);
        tray.Hide();
        Assert.False(tray.IsVisible);

        tray.Dispose();
        Assert.Throws<ObjectDisposedException>(tray.Show);
        Assert.Throws<ObjectDisposedException>(tray.Hide);
        tray.Dispose();
    }

    [AvaloniaFact]
    public void The_real_tray_adapter_carries_its_menu_and_refuses_empty_labels()
    {
        using var tray = new WindowsTrayService("AP Reelume", "Open", "Exit");

        var opened = 0;
        var exited = 0;
        tray.OpenRequested += (_, _) => opened++;
        tray.ExitRequested += (_, _) => exited++;

        Assert.Equal(2, tray.Menu.Items.Count);
        Assert.Equal(["Open", "Exit"], tray.Menu.Items.OfType<NativeMenuItem>().Select(item => item.Header));
        foreach (var item in tray.Menu.Items.OfType<NativeMenuItem>())
        {
            item.Command!.Execute(null);
        }

        Assert.Equal(1, opened);
        Assert.Equal(1, exited);
        Assert.Throws<ArgumentException>(() => new WindowsTrayService(" ", "Open", "Exit"));
        Assert.Throws<ArgumentException>(() => new WindowsTrayService("AP Reelume", " ", "Exit"));
        Assert.Throws<ArgumentException>(() => new WindowsTrayService("AP Reelume", "Open", " "));
    }

    [Fact]
    public void The_stored_choices_survive_a_restart_and_a_contradictory_file_is_repaired()
    {
        var directory = Directory.CreateTempSubdirectory("reelume-lifecycle");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            var settings = new StoredLifecycleSettings(new JsonSettingsStore(path));

            Assert.Equal(LifecyclePreferences.Default, settings.Current);

            settings.Save(AppLifecyclePolicy.WithCloseBehavior(
                AppLifecyclePolicy.WithStartup(
                    AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
                    isRequested: true,
                    hasConsent: true),
                CloseBehavior.MinimizeToTray));

            var reopened = new StoredLifecycleSettings(new JsonSettingsStore(path)).Current;
            Assert.True(reopened.TrayEnabled);
            Assert.True(reopened.StartWithWindows);
            Assert.Equal(CloseBehavior.MinimizeToTray, reopened.CloseBehavior);

            // A hand-edited file that asks to close to a tray that does not exist is repaired on read.
            File.WriteAllText(
                path,
                """
                {
                  "lifecycle.trayEnabled": false,
                  "lifecycle.startWithWindows": true,
                  "lifecycle.closeBehavior": "MinimizeToTray"
                }
                """);
            var repaired = new StoredLifecycleSettings(new JsonSettingsStore(path)).Current;
            Assert.False(repaired.TrayEnabled);
            Assert.True(repaired.StartWithWindows);
            Assert.Equal(CloseBehavior.Exit, repaired.CloseBehavior);

            Assert.Throws<ArgumentNullException>(() => new StoredLifecycleSettings(null!));
            Assert.Throws<ArgumentNullException>(() =>
                new StoredLifecycleSettings(new JsonSettingsStore(path)).Save(null!));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void The_use_case_refuses_missing_collaborators()
    {
        var tray = new RecordingTrayService();
        var settings = new InMemoryLifecycleSettings(LifecyclePreferences.Default);
        static Task Nothing() => Task.CompletedTask;

        Assert.Throws<ArgumentNullException>(() =>
            new CloseApplication(null!, tray, Nothing, Nothing, () => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new CloseApplication(settings, null!, Nothing, Nothing, () => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new CloseApplication(settings, tray, null!, Nothing, () => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new CloseApplication(settings, tray, Nothing, null!, () => { }));
        Assert.Throws<ArgumentNullException>(() =>
            new CloseApplication(settings, tray, Nothing, Nothing, null!));
    }

    private static CloseApplication Build(
        List<string> log,
        LifecyclePreferences preferences,
        out RecordingTrayService tray)
    {
        var created = new RecordingTrayService();
        tray = created;
        return new CloseApplication(
            new InMemoryLifecycleSettings(preferences),
            created,
            persistProgress: () =>
            {
                log.Add("progress");
                return Task.CompletedTask;
            },
            stopPlayback: () =>
            {
                log.Add("stop-playback");
                return Task.CompletedTask;
            },
            hideWindow: () => log.Add("hide-to-tray"),
            exitApplication: () => log.Add("exit"));
    }

    private sealed class InMemoryLifecycleSettings(LifecyclePreferences preferences) : ILifecycleSettings
    {
        public LifecyclePreferences Current { get; private set; } = preferences;

        public void Save(LifecyclePreferences updated) => Current = updated;
    }

    private sealed class RecordingTrayService : ITrayService
    {
        public bool IsVisible { get; private set; }

        public int ShowCalls { get; private set; }

        public int HideCalls { get; private set; }

        public int OpenRequests { get; private set; }

        public int ExitRequests { get; private set; }

        public event EventHandler? OpenRequested
        {
            add { }
            remove { }
        }

        public event EventHandler? ExitRequested
        {
            add { }
            remove { }
        }

        public void Show()
        {
            if (IsVisible)
            {
                return;
            }

            IsVisible = true;
            ShowCalls++;
        }

        public void Hide()
        {
            if (!IsVisible)
            {
                return;
            }

            IsVisible = false;
            HideCalls++;
        }
    }
}
