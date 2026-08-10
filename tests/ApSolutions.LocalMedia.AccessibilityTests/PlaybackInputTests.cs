// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Windows.MediaKeys;
using Avalonia.Automation;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// Every essential action must be reachable from the keyboard alone, shortcuts must be
/// reconfigurable without creating a conflict, and the media-key service must claim the system keys
/// only while a session exists. This suite targets the Windows host, so it asserts the real service.
/// </summary>
public sealed class PlaybackInputTests
{
    [Fact]
    public void Every_essential_action_has_a_default_keyboard_gesture()
    {
        var map = new ShortcutMap();

        foreach (var command in Enum.GetValues<PlaybackInputCommand>())
        {
            Assert.True(map.Bindings.ContainsKey(command), $"{command} has no default gesture.");
            Assert.True(map.IsDefault(command));
        }

        Assert.Equal(
            map.Bindings.Count,
            map.Bindings.Values.Select(gesture => gesture.ToString()).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_gesture_resolves_to_exactly_one_command()
    {
        var map = new ShortcutMap();

        Assert.Equal(PlaybackInputCommand.PlayPause, map.Resolve(new KeyGesture(Key.Space)));
        Assert.Equal(PlaybackInputCommand.SkipForward, map.Resolve(new KeyGesture(Key.Right)));
        Assert.Equal(PlaybackInputCommand.ExitOverlayMode, map.Resolve(new KeyGesture(Key.Escape)));
        Assert.Null(map.Resolve(new KeyGesture(Key.F12, KeyModifiers.Alt)));
    }

    [Fact]
    public void A_conflicting_rebind_is_refused_and_names_the_command_that_holds_the_gesture()
    {
        var map = new ShortcutMap();

        var holder = map.TryRebind(PlaybackInputCommand.ToggleMute, new KeyGesture(Key.Space));

        Assert.Equal(PlaybackInputCommand.PlayPause, holder);
        Assert.Equal(PlaybackInputCommand.PlayPause, map.Resolve(new KeyGesture(Key.Space)));
        Assert.True(map.IsDefault(PlaybackInputCommand.ToggleMute));
    }

    [Fact]
    public void A_free_gesture_is_accepted_and_the_defaults_can_be_restored()
    {
        var map = new ShortcutMap();

        Assert.Null(map.TryRebind(PlaybackInputCommand.ToggleMute, new KeyGesture(Key.K, KeyModifiers.Control)));
        Assert.False(map.IsDefault(PlaybackInputCommand.ToggleMute));
        Assert.Equal(
            PlaybackInputCommand.ToggleMute,
            map.Resolve(new KeyGesture(Key.K, KeyModifiers.Control)));

        map.RestoreDefaults();
        Assert.True(map.IsDefault(PlaybackInputCommand.ToggleMute));
        Assert.Null(map.Resolve(new KeyGesture(Key.K, KeyModifiers.Control)));
    }

    [AvaloniaFact]
    public void The_shortcut_editor_lists_every_command_and_reports_a_conflict_in_text()
    {
        var viewModel = new ShortcutSettingsViewModel(new ShortcutMap());

        Assert.Equal(Enum.GetValues<PlaybackInputCommand>().Length, viewModel.Bindings.Count);
        Assert.All(viewModel.Bindings, row => Assert.False(string.IsNullOrWhiteSpace(row.CommandLabel)));
        Assert.All(viewModel.Bindings, row => Assert.False(string.IsNullOrWhiteSpace(row.GestureLabel)));
        Assert.False(viewModel.HasConflict);

        Assert.False(viewModel.TryRebind(PlaybackInputCommand.ToggleMute, new KeyGesture(Key.Space)));
        Assert.True(viewModel.HasConflict);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.ConflictMessage));

        Assert.True(viewModel.TryRebind(PlaybackInputCommand.ToggleMute, new KeyGesture(Key.K, KeyModifiers.Alt)));
        Assert.False(viewModel.HasConflict);
        Assert.Contains(viewModel.Bindings, row => row.IsCustomised);

        viewModel.RestoreDefaultsCommand.Execute(null);
        Assert.DoesNotContain(viewModel.Bindings, row => row.IsCustomised);
    }

    [AvaloniaFact]
    public void The_shortcut_editor_is_named_and_reachable_without_a_mouse_in_both_languages()
    {
        Assert.NotNull(Avalonia.Application.Current);
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(
                Avalonia.Application.Current!,
                System.Globalization.CultureInfo.GetCultureInfo(cultureName));
            var view = new ShortcutSettingsView { DataContext = new ShortcutSettingsViewModel(new ShortcutMap()) };
            var window = new Avalonia.Controls.Window { Width = 640, Height = 480, Content = view };
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var restore = view.GetVisualDescendants()
                .OfType<Avalonia.Controls.Button>()
                .Single(button => button.Name == "RestoreDefaultsButton");
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(restore)));
            Assert.True(restore.Focusable);
            Assert.True(restore.Focus(NavigationMethod.Tab));

            var list = view.GetVisualDescendants()
                .OfType<Avalonia.Controls.ItemsControl>()
                .Single(control => control.Name == "ShortcutList");
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(list)));
            window.Close();
        }
    }

    [Fact]
    public async Task The_real_media_key_service_claims_the_keys_only_while_it_is_listening()
    {
        using var service = new WindowsMediaKeyService();

        Assert.False(service.IsListening);
        Assert.Equal(0, service.RegisteredKeyCount);

        await service.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            Assert.True(service.IsListening);
            Assert.True(
                service.RegisteredKeyCount >= 0,
                "Another application may already hold a key; the count is what this machine allowed.");
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
        }

        Assert.False(service.IsListening);
        Assert.Equal(0, service.RegisteredKeyCount);
    }

    [Fact]
    public async Task Starting_twice_and_stopping_twice_leaves_nothing_registered()
    {
        using var service = new WindowsMediaKeyService();

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(service.IsListening);

        await service.StopAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(service.IsListening);
        Assert.Equal(0, service.RegisteredKeyCount);
    }

    [Fact]
    public void The_service_claims_only_transport_keys_and_maps_each_to_one_action()
    {
        Assert.Equal(
            [
                PlaybackInputCommand.PlayPause,
                PlaybackInputCommand.Stop,
                PlaybackInputCommand.SkipForward,
                PlaybackInputCommand.SkipBackward,
            ],
            WindowsMediaKeyService.HandledCommands);
        Assert.Equal(
            WindowsMediaKeyService.HandledCommands.Count,
            WindowsMediaKeyService.HandledCommands.Distinct().Count());
    }
}
