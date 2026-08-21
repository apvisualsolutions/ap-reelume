// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Domain.Lifecycle;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The consent surface for the tray and Windows startup. Both are off until this screen is used, and
/// the screen states in words what it is about to change.
/// </summary>
public sealed class LifecycleSettingsTests
{
    [Fact]
    public void The_screen_opens_with_everything_switched_off()
    {
        var viewModel = new LifecycleSettingsViewModel(
            new InMemorySettings(LifecyclePreferences.Default),
            new StubStartupService());

        Assert.False(viewModel.TrayEnabled);
        Assert.False(viewModel.StartWithWindows);
        Assert.False(viewModel.MinimizeToTrayOnClose);
        Assert.False(viewModel.CanMinimizeToTray);
    }

    [Fact]
    public void Turning_the_tray_on_unlocks_closing_to_it_and_turning_it_off_locks_it_again()
    {
        var settings = new InMemorySettings(LifecyclePreferences.Default);
        var viewModel = new LifecycleSettingsViewModel(settings, new StubStartupService());

        viewModel.TrayEnabled = true;
        Assert.True(viewModel.CanMinimizeToTray);

        viewModel.MinimizeToTrayOnClose = true;
        Assert.Equal(CloseBehavior.MinimizeToTray, settings.Current.CloseBehavior);

        viewModel.TrayEnabled = false;
        Assert.False(viewModel.CanMinimizeToTray);
        Assert.False(viewModel.MinimizeToTrayOnClose);
        Assert.Equal(CloseBehavior.Exit, settings.Current.CloseBehavior);
    }

    [Fact]
    public void Startup_only_changes_after_the_consent_is_given_and_is_always_reversible()
    {
        var settings = new InMemorySettings(LifecyclePreferences.Default);
        var startup = new StubStartupService();
        var viewModel = new LifecycleSettingsViewModel(settings, startup);

        viewModel.StartWithWindows = true;
        Assert.False(viewModel.StartWithWindows);
        Assert.True(viewModel.IsStartupConsentPending);
        Assert.Equal(StartupEntryState.Absent, startup.State);

        viewModel.GrantStartupConsentCommand.Execute(null);
        Assert.True(viewModel.StartWithWindows);
        Assert.False(viewModel.IsStartupConsentPending);
        Assert.Equal(StartupEntryState.Present, startup.State);
        Assert.True(settings.Current.StartWithWindows);

        viewModel.StartWithWindows = false;
        Assert.False(viewModel.StartWithWindows);
        Assert.False(viewModel.IsStartupConsentPending);
        Assert.Equal(StartupEntryState.Absent, startup.State);
        Assert.False(settings.Current.StartWithWindows);
    }

    [Fact]
    public void Declining_the_consent_leaves_the_registry_untouched()
    {
        var settings = new InMemorySettings(LifecyclePreferences.Default);
        var startup = new StubStartupService();
        var viewModel = new LifecycleSettingsViewModel(settings, startup);

        viewModel.StartWithWindows = true;
        viewModel.DeclineStartupConsentCommand.Execute(null);

        Assert.False(viewModel.StartWithWindows);
        Assert.False(viewModel.IsStartupConsentPending);
        Assert.Equal(StartupEntryState.Absent, startup.State);
        Assert.Equal(0, startup.EnableCalls);
    }

    [Fact]
    public void A_stored_choice_survives_a_restart_and_an_invalid_entry_is_repaired_on_the_way_in()
    {
        var stored = AppLifecyclePolicy.WithStartup(
            AppLifecyclePolicy.WithTray(LifecyclePreferences.Default, true),
            isRequested: true,
            hasConsent: true);
        var settings = new InMemorySettings(stored);
        var startup = new StubStartupService { State = StartupEntryState.Invalid };

        var viewModel = new LifecycleSettingsViewModel(settings, startup);

        Assert.True(viewModel.TrayEnabled);
        Assert.True(viewModel.StartWithWindows);
        Assert.Equal(1, startup.RepairCalls);
        Assert.Equal(StartupEntryState.Present, startup.State);
    }

    [Fact]
    public void Setting_a_value_to_what_it_already_is_changes_nothing()
    {
        var settings = new InMemorySettings(LifecyclePreferences.Default);
        var startup = new StubStartupService();
        var viewModel = new LifecycleSettingsViewModel(settings, startup)
        {
            TrayEnabled = false,
            StartWithWindows = false,
            MinimizeToTrayOnClose = false,
        };

        Assert.Equal(LifecyclePreferences.Default, settings.Current);
        Assert.Equal(0, startup.EnableCalls);
        Assert.False(viewModel.IsStartupConsentPending);

        // Confirming a consent nobody asked for does nothing either.
        viewModel.GrantStartupConsentCommand.Execute(null);
        Assert.False(viewModel.StartWithWindows);
        Assert.Equal(0, startup.EnableCalls);
    }

    [Fact]
    public void The_view_model_refuses_missing_collaborators()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleSettingsViewModel(null!, new StubStartupService()));
        Assert.Throws<ArgumentNullException>(() =>
            new LifecycleSettingsViewModel(new InMemorySettings(LifecyclePreferences.Default), null!));
    }

    [AvaloniaFact]
    public void Every_control_is_named_focusable_and_states_its_choice_in_words()
    {
        Assert.NotNull(Avalonia.Application.Current);
        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var view = new LifecycleSettingsView
            {
                DataContext = new LifecycleSettingsViewModel(
                    new InMemorySettings(LifecyclePreferences.Default),
                    new StubStartupService()),
            };
            var window = new Window { Width = 900, Height = 700, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var controls = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control is CheckBox or Button)
                .Where(control => control.TemplatedParent is null && control.IsEffectivelyVisible)
                .ToArray();
            Assert.NotEmpty(controls);
            Assert.All(controls, control =>
            {
                Assert.True(control.Focusable, $"{control.GetType().Name} cannot take focus.");
                Assert.False(
                    string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                    $"{control.GetType().Name} has no accessible name.");
            });

            window.Close();
        }
    }

    /// <summary>
    /// Every word on this screen comes from a resource, and a symbol is not a word.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This asked for no literal of any kind until 2026-08-21, when the "no tray on this system"
    /// notice took the warning grammar the rest of the tree uses and brought its <c>⚠</c> with it.
    /// The glyph is literal in every other view that carries it, and none of them has a gate saying
    /// otherwise, so this one view was stricter than the tree by accident rather than by decision.
    /// </para>
    /// <para>
    /// <b>What the gate protects is untranslated words</b>, and that is what it now says: a literal is
    /// allowed only if it holds <b>no letter at all</b>. A word cannot slip through as a symbol — one
    /// letter anywhere in the value fails it — and it is asserted alongside the whitelist so an empty
    /// run of symbols cannot make this pass by measuring nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public void Every_visible_word_on_the_lifecycle_screen_comes_from_a_resource()
    {
        var view = Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Settings",
            "LifecycleSettingsView.axaml");
        var document = XDocument.Load(view);

        var painted = document.Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "ToolTip.Tip" or "Header")
            .Where(attribute => !attribute.Value.TrimStart().StartsWith('{'))
            .Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
            .ToArray();

        var words = painted.Where(literal => literal.Split('=', 2)[1].Any(char.IsLetter)).ToArray();
        Assert.Empty(words);

        // The symbols that are left are asserted as present, because "no words" is also what a screen
        // with nothing painted on it would report.
        Assert.NotEmpty(painted);
    }

    private sealed class InMemorySettings(LifecyclePreferences preferences) : ILifecycleSettings
    {
        public LifecyclePreferences Current { get; private set; } = preferences;

        public void Save(LifecyclePreferences updated) => Current = updated;
    }

    private sealed class StubStartupService : IStartupService
    {
        public StartupEntryState State { get; set; } = StartupEntryState.Absent;

        public int EnableCalls { get; private set; }

        public int RepairCalls { get; private set; }

        public string ExpectedCommand => "\"reelume.exe\"";

        public StartupEntryState Inspect() => State;

        public void Enable()
        {
            EnableCalls++;
            State = StartupEntryState.Present;
        }

        public void Disable() => State = StartupEntryState.Absent;

        public bool Repair()
        {
            RepairCalls++;
            if (State != StartupEntryState.Invalid)
            {
                return false;
            }

            State = StartupEntryState.Present;
            return true;
        }
    }
}
