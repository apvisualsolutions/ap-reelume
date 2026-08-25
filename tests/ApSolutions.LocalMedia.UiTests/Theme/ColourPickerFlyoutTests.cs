// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The three colour pickers, opened rather than merely declared.
/// </summary>
/// <remarks>
/// A picker lives behind a flyout, and a flyout that is never opened is markup nothing builds: both
/// pages fell below their coverage floor the moment the pickers landed, which is the ratchet saying
/// what a reader could not — that three grids of forty-eight cells, three sliders each and a readout
/// had arrived and no test had ever seen one of them on screen.
/// </remarks>
[Collection("ThemeVariant")]
public sealed class ColourPickerFlyoutTests
{
    [AvaloniaFact]
    public void The_two_subtitle_pickers_open_with_the_grid_the_domain_owns()
    {
        var page = new SubtitleStyleView { DataContext = new SubtitleStyleViewModel(new NoPreferences()) };
        var window = new Window { Width = 900, Height = 800, Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var opened = OpenEveryFlyout(page);

        Assert.Equal(2, opened);
        Assert.Equal(AccentPalette.Grid, SubtitleStyleViewModel.ColourGrid);
        window.Close();
    }

    [AvaloniaFact]
    public void The_accent_picker_opens_with_the_same_grid()
    {
        var page = new AppearanceSettingsView
        {
            DataContext = new AppearanceSettingsViewModel(new StubTheme(), null, null),
        };
        var window = new Window { Width = 900, Height = 900, Content = page };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var opened = OpenEveryFlyout(page);

        Assert.Equal(1, opened);
        Assert.Equal(AccentPalette.Grid, AppearanceSettingsViewModel.ColourGrid);
        window.Close();
    }

    /// <summary>Opens every flyout the page declares and counts them, then closes each again.</summary>
    private static int OpenEveryFlyout(Control page)
    {
        var buttons = page.GetLogicalDescendants()
            .OfType<Button>()
            .Where(button => button.Flyout is not null)
            .ToArray();
        foreach (var button in buttons)
        {
            button.Flyout!.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            button.Flyout.Hide();
            Dispatcher.UIThread.RunJobs();
        }

        return buttons.Length;
    }

    private sealed class NoPreferences : IPlaybackPreferenceRepository
    {
        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PlaybackPreference?>(null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubTheme : IThemeService
    {
        public ThemePreference CurrentPreference => ThemePreference.System;

        public ThemeVariant PlayerThemeVariant => ThemeVariant.Dark;

        public bool AnimationsEnabled => true;

        public TimeSpan MotionDuration => TimeSpan.FromMilliseconds(150);

        public void Apply(ThemePreference preference)
        {
        }

        public bool TryApplyBackdrop(Window window) => false;
    }
}
