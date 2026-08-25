// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Styling;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The nine rows the appearance page grew on 2026-08-25, exercised through the page rather than
/// through the service under it.
/// </summary>
/// <remarks>
/// They arrived with the page that draws them and with nothing that pressed them, and the coverage
/// ratchet is what said so: the file fell from 100/84 to 91/75 in the run that landed them. What is
/// asserted here is the round trip a person makes — move a row, read it back, and see the page
/// announce it — because a setter that reaches a service nobody reads is the defect this repository
/// keeps finding.
/// </remarks>
public sealed class AppearanceRowsTests
{
    [Fact]
    public void Every_row_stores_what_it_is_given_and_reads_it_back()
    {
        var appearance = new RecordingAppearance();
        var page = new AppearanceSettingsViewModel(new StubTheme(), languageService: null, appearance);
        var announced = new List<string>();
        page.PropertyChanged += (_, args) => announced.Add(args.PropertyName ?? string.Empty);

        page.FollowsWindowsTheme = false;
        page.Mica = false;
        page.TintPercent = 42.4;
        page.CoverWidth = 199.6;
        page.CoverTitles = false;
        page.Animations = false;

        Assert.False(page.FollowsWindowsTheme);
        Assert.False(page.Mica);
        Assert.Equal(42, page.TintPercent);
        Assert.Equal(200, page.CoverWidth);
        Assert.False(page.CoverTitles);
        Assert.False(page.Animations);
        Assert.Equal(6, appearance.Applied);

        // Every row again on every change, because they are not independent: what the page shows
        // for one of them can move when another does.
        Assert.Contains(nameof(page.AccentHex), announced);
        Assert.Contains(nameof(page.CompactCue), announced);
    }

    [Theory]
    [InlineData(InterfaceDensity.Compact)]
    [InlineData(InterfaceDensity.Comfortable)]
    [InlineData(InterfaceDensity.Roomy)]
    public void The_density_row_lights_exactly_the_pill_that_is_in_force(InterfaceDensity density)
    {
        var page = new AppearanceSettingsViewModel(new StubTheme(), null, new RecordingAppearance());

        page.ApplyDensityCommand.Execute(density);

        Assert.Equal(density, page.Density);
        Assert.Equal(
            1,
            new[] { page.CompactCue, page.ComfortableCue, page.RoomyCue }.Count(cue => cue == "●"));
    }

    [Theory]
    [InlineData(CornerRounding.Sharp)]
    [InlineData(CornerRounding.Soft)]
    [InlineData(CornerRounding.VeryRound)]
    public void The_rounding_row_lights_exactly_the_pill_that_is_in_force(CornerRounding rounding)
    {
        var page = new AppearanceSettingsViewModel(new StubTheme(), null, new RecordingAppearance());

        page.ApplyRoundingCommand.Execute(rounding);

        Assert.Equal(rounding, page.Rounding);
        Assert.Equal(
            1,
            new[] { page.SharpCue, page.SoftCue, page.VeryRoundCue }.Count(cue => cue == "●"));
    }

    [Fact]
    public void A_pill_command_refuses_a_value_that_is_not_one_of_its_own()
    {
        var page = new AppearanceSettingsViewModel(new StubTheme(), null, new RecordingAppearance());

        Assert.False(page.ApplyDensityCommand.CanExecute("roomy"));
        Assert.False(page.ApplyDensityCommand.CanExecute((InterfaceDensity)99));
        Assert.False(page.ApplyRoundingCommand.CanExecute(null));
        page.ApplyDensityCommand.Execute("roomy");
        page.ApplyRoundingCommand.Execute((CornerRounding)99);

        Assert.Equal(InterfaceDensity.Comfortable, page.Density);
        Assert.Equal(CornerRounding.Soft, page.Rounding);
    }

    [Fact]
    public void The_accent_row_lights_one_swatch_and_calls_anything_else_custom()
    {
        var page = new AppearanceSettingsViewModel(new StubTheme(), null, new RecordingAppearance());

        page.ApplyAccentCommand.Execute(AccentPalette.Presets[3]);

        Assert.Equal(AccentPalette.Presets[3].ToUpperInvariant(), page.AccentHex);
        Assert.False(page.IsCustomAccent);
        Assert.Equal(
            1,
            Flags(page).Count(flag => flag));

        // The picker's three, on the scales the domain actually uses: a hue in degrees and the other
        // two in per cent. What they promise is that the colour moved and is still an accent — not
        // the exact number back, because eight bits per channel do not hold every triple.
        page.AccentLightness = Math.Clamp(page.AccentLightness + 20, 0, 100);
        Assert.True(page.IsCustomAccent);
        Assert.DoesNotContain(true, Flags(page));
        Assert.True(AccentPalette.IsAccent(page.AccentHex));

        var hue = page.AccentHue;
        page.AccentHue = (hue + 120) % 360;
        Assert.NotEqual(hue, page.AccentHue);

        var saturation = page.AccentSaturation;
        page.AccentSaturation = Math.Clamp(saturation - 30, 0, 100);
        Assert.True(page.AccentSaturation < saturation);
        Assert.True(AccentPalette.IsAccent(page.AccentHex));
    }

    [Fact]
    public void The_six_swatches_and_the_grid_behind_them_are_the_domains_own()
    {
        Assert.Equal(AccentPalette.Presets, AppearanceSettingsViewModel.AccentSwatches);
        Assert.Equal(AccentPalette.Grid, AppearanceSettingsViewModel.ColourGrid);
        Assert.Equal(6, AppearanceSettingsViewModel.AccentSwatches.Count);
    }

    [Fact]
    public void The_accent_command_refuses_what_is_not_a_colour()
    {
        var page = new AppearanceSettingsViewModel(new StubTheme(), null, new RecordingAppearance());
        var before = page.AccentHex;

        Assert.False(page.ApplyAccentCommand.CanExecute("azul"));
        Assert.False(page.ApplyAccentCommand.CanExecute(null));
        page.ApplyAccentCommand.Execute("azul");
        page.ApplyAccentCommand.Execute(17);

        Assert.Equal(before, page.AccentHex);
    }

    /// <summary>
    /// The page without an appearance service, which several suites mount and which has to answer.
    /// </summary>
    [Fact]
    public void A_page_with_nowhere_to_store_a_preference_reads_its_defaults_and_writes_nothing()
    {
        var page = new AppearanceSettingsViewModel(new StubTheme());
        var defaults = new AppearanceOptions();

        page.Mica = !defaults.Mica;
        page.Density = InterfaceDensity.Roomy;
        page.AccentHue = 200;
        page.AccentSaturation = 50;
        page.AccentLightness = 50;

        Assert.Equal(defaults.Mica, page.Mica);
        Assert.Equal(defaults.Density, page.Density);
        Assert.Equal(defaults.Accent.ToUpperInvariant(), page.AccentHex);
        Assert.True(page.PlayerSurfaceIsFixed);
    }

    private static bool[] Flags(AppearanceSettingsViewModel page) =>
    [
        page.IsFirstAccent,
        page.IsSecondAccent,
        page.IsThirdAccent,
        page.IsFourthAccent,
        page.IsFifthAccent,
        page.IsSixthAccent,
    ];

    /// <summary>Holds the set and counts how often it was written, which is the whole contract.</summary>
    private sealed class RecordingAppearance : IAppearanceService
    {
        public AppearanceOptions Current { get; private set; } = new();

        public int Applied { get; private set; }

        public int Reapplied { get; private set; }

        public bool WantsBackdrop => Current.Mica;

        public void Apply(AppearanceOptions options)
        {
            Current = options;
            Applied++;
        }

        public void Reapply() => Reapplied++;
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
