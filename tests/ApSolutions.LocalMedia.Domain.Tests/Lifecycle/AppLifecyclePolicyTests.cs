// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Lifecycle;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Lifecycle;

/// <summary>
/// The rules that keep the tray and Windows startup opt-in. Everything here is a pure decision: the
/// adapters only carry it out.
/// </summary>
public sealed class AppLifecyclePolicyTests
{
    [Fact]
    public void Nothing_is_on_until_the_person_turns_it_on()
    {
        var defaults = LifecyclePreferences.Default;

        Assert.False(defaults.TrayEnabled);
        Assert.False(defaults.StartWithWindows);
        Assert.Equal(CloseBehavior.Exit, defaults.CloseBehavior);
    }

    [Fact]
    public void Closing_to_the_tray_is_only_possible_while_the_tray_exists()
    {
        var withoutTray = AppLifecyclePolicy.WithCloseBehavior(
            LifecyclePreferences.Default,
            CloseBehavior.MinimizeToTray);
        Assert.Equal(CloseBehavior.Exit, withoutTray.CloseBehavior);

        var withTray = AppLifecyclePolicy.WithCloseBehavior(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, isEnabled: true),
            CloseBehavior.MinimizeToTray);
        Assert.Equal(CloseBehavior.MinimizeToTray, withTray.CloseBehavior);
    }

    [Fact]
    public void Turning_the_tray_off_gives_the_close_button_back_its_meaning()
    {
        var minimising = AppLifecyclePolicy.WithCloseBehavior(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, isEnabled: true),
            CloseBehavior.MinimizeToTray);

        var closed = AppLifecyclePolicy.WithTray(minimising, isEnabled: false);

        Assert.False(closed.TrayEnabled);
        Assert.Equal(CloseBehavior.Exit, closed.CloseBehavior);
    }

    [Fact]
    public void Starting_with_Windows_needs_consent_and_giving_it_up_never_does()
    {
        var refused = AppLifecyclePolicy.WithStartup(
            LifecyclePreferences.Default,
            isRequested: true,
            hasConsent: false);
        Assert.False(refused.StartWithWindows);

        var granted = AppLifecyclePolicy.WithStartup(
            LifecyclePreferences.Default,
            isRequested: true,
            hasConsent: true);
        Assert.True(granted.StartWithWindows);

        var withdrawn = AppLifecyclePolicy.WithStartup(granted, isRequested: false, hasConsent: false);
        Assert.False(withdrawn.StartWithWindows);
    }

    [Fact]
    public void Enabling_and_disabling_twice_lands_exactly_where_once_does()
    {
        var once = AppLifecyclePolicy.WithStartup(LifecyclePreferences.Default, true, true);
        var twice = AppLifecyclePolicy.WithStartup(once, true, true);
        Assert.Equal(once, twice);

        var offOnce = AppLifecyclePolicy.WithStartup(twice, false, true);
        var offTwice = AppLifecyclePolicy.WithStartup(offOnce, false, true);
        Assert.Equal(offOnce, offTwice);

        var trayOnce = AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true);
        Assert.Equal(trayOnce, AppLifecyclePolicy.WithTray(trayOnce, true));
    }

    [Fact]
    public void Closing_always_writes_the_progress_before_it_does_anything_else()
    {
        foreach (var hasPlayback in new[] { true, false })
        {
            foreach (var preferences in new[]
            {
                LifecyclePreferences.Default,
                AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
                AppLifecyclePolicy.WithCloseBehavior(
                    AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
                    CloseBehavior.MinimizeToTray),
            })
            {
                var decision = AppLifecyclePolicy.ResolveClose(preferences, hasPlayback);
                Assert.True(decision.PersistProgressFirst);
            }
        }
    }

    [Fact]
    public void Closing_without_the_tray_exits_and_stops_whatever_was_playing()
    {
        var decision = AppLifecyclePolicy.ResolveClose(LifecyclePreferences.Default, hasActivePlayback: true);

        Assert.True(decision.PersistProgressFirst);
        Assert.True(decision.StopPlayback);
        Assert.True(decision.ExitApplication);
        Assert.False(decision.HideToTray);
    }

    [Fact]
    public void Closing_to_the_tray_hides_the_window_and_leaves_the_session_playing()
    {
        var preferences = AppLifecyclePolicy.WithCloseBehavior(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
            CloseBehavior.MinimizeToTray);

        var decision = AppLifecyclePolicy.ResolveClose(preferences, hasActivePlayback: true);

        Assert.True(decision.PersistProgressFirst);
        Assert.False(decision.StopPlayback);
        Assert.False(decision.ExitApplication);
        Assert.True(decision.HideToTray);
    }

    [Fact]
    public void An_enabled_tray_that_still_closes_by_exiting_really_exits()
    {
        var preferences = AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, isEnabled: true);

        var decision = AppLifecyclePolicy.ResolveClose(preferences, hasActivePlayback: false);

        Assert.True(decision.ExitApplication);
        Assert.False(decision.HideToTray);
        Assert.False(decision.StopPlayback);
    }

    [Fact]
    public void A_stored_state_that_contradicts_itself_is_repaired_rather_than_trusted()
    {
        var impossible = new LifecyclePreferences
        {
            TrayEnabled = false,
            StartWithWindows = true,
            CloseBehavior = CloseBehavior.MinimizeToTray,
        };

        var repaired = AppLifecyclePolicy.Normalize(impossible);

        Assert.Equal(CloseBehavior.Exit, repaired.CloseBehavior);
        Assert.False(repaired.TrayEnabled);
        Assert.True(repaired.StartWithWindows);
        Assert.Equal(repaired, AppLifecyclePolicy.Normalize(repaired));
    }

    [Fact]
    public void The_policy_refuses_a_missing_state()
    {
        Assert.Throws<ArgumentNullException>(() => AppLifecyclePolicy.Normalize(null!));
        Assert.Throws<ArgumentNullException>(() => AppLifecyclePolicy.WithTray(null!, true));
        Assert.Throws<ArgumentNullException>(() => AppLifecyclePolicy.WithStartup(null!, true, true));
        Assert.Throws<ArgumentNullException>(() =>
            AppLifecyclePolicy.WithCloseBehavior(null!, CloseBehavior.Exit));
        Assert.Throws<ArgumentNullException>(() => AppLifecyclePolicy.ResolveClose(null!, false));
    }
}
