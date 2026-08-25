// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The two colour rows the subtitle page grew on 2026-08-25: six swatches, a picker, and the flags
/// that say which one is in force.
/// </summary>
/// <remarks>
/// They arrived with the page and with nothing that pressed them; the coverage ratchet is what said
/// so, the file falling from 100/93 to 90/85 in the run that landed them. The one thing worth
/// asserting more than once is that <b>the opacity survives a change of colour</b>: the stored
/// ground is <c>#CC000000</c> and its swatch is <c>#000000</c>, so a picker that wrote all four
/// channels would silently make every background opaque.
/// </remarks>
public sealed class SubtitleColourPickerTests
{
    [Fact]
    public void A_swatch_press_changes_the_three_channels_and_leaves_the_opacity_alone()
    {
        var page = new SubtitleStyleViewModel(new InMemoryPreferences());
        var beforeOpacity = page.BackgroundHex[..3];

        page.ApplyBackgroundCommand.Execute(SubtitleStyleViewModel.BackgroundSwatches[2]);
        page.ApplyForegroundCommand.Execute(SubtitleStyleViewModel.ForegroundSwatches[2]);

        Assert.Equal(beforeOpacity, page.BackgroundHex[..3]);
        Assert.True(page.IsThirdBackground);
        Assert.True(page.IsThirdForeground);
        Assert.False(page.IsCustomBackground);
        Assert.False(page.IsCustomForeground);
    }

    [Fact]
    public void Exactly_one_swatch_of_each_row_is_the_one_in_force()
    {
        var page = new SubtitleStyleViewModel(new InMemoryPreferences());

        for (var index = 0; index < SubtitleStyleViewModel.ForegroundSwatches.Count; index++)
        {
            page.ApplyForegroundCommand.Execute(SubtitleStyleViewModel.ForegroundSwatches[index]);
            page.ApplyBackgroundCommand.Execute(SubtitleStyleViewModel.BackgroundSwatches[index]);

            Assert.Equal(index, Array.IndexOf(ForegroundFlags(page), true));
            Assert.Equal(index, Array.IndexOf(BackgroundFlags(page), true));
        }
    }

    [Fact]
    public void A_colour_that_is_none_of_the_six_is_custom_in_both_rows()
    {
        var page = new SubtitleStyleViewModel(new InMemoryPreferences())
        {
            ForegroundHex = "#FF7B1FA2",
            BackgroundHex = "#801B5E20",
        };

        Assert.True(page.IsCustomForeground);
        Assert.True(page.IsCustomBackground);
        Assert.DoesNotContain(true, ForegroundFlags(page));
        Assert.DoesNotContain(true, BackgroundFlags(page));
    }

    [Fact]
    public void The_pickers_three_take_the_colour_apart_and_put_it_back()
    {
        var page = new SubtitleStyleViewModel(new InMemoryPreferences());

        // Lightness first, and that is not a detail: the ink starts white, and at full lightness a
        // colour has no hue and no saturation to move — every triple lands on white. A picker is
        // three sliders over one colour, so the order a person moves them in is part of what they
        // do.
        page.ForegroundLightness = 60;
        page.ForegroundSaturation = 80;
        page.ForegroundHue = 120;
        Assert.Equal("#FF", page.ForegroundHex[..3]);
        Assert.InRange(page.ForegroundLightness, 55, 65);
        Assert.InRange(page.ForegroundSaturation, 70, 90);
        Assert.InRange(page.ForegroundHue, 110, 130);

        page.BackgroundLightness = 20;
        page.BackgroundSaturation = 40;
        page.BackgroundHue = 210;
        Assert.Equal("#CC", page.BackgroundHex[..3]);
        Assert.InRange(page.BackgroundHue, 200, 220);
        Assert.InRange(page.BackgroundLightness, 15, 25);
    }

    /// <summary>
    /// A colour with no alpha in front of it, which is what a stored seven-character value is.
    /// </summary>
    [Fact]
    public void A_colour_stored_without_an_opacity_keeps_none_when_a_swatch_replaces_it()
    {
        var page = new SubtitleStyleViewModel(new InMemoryPreferences())
        {
            ForegroundHex = "#123456",
        };

        page.ApplyForegroundCommand.Execute(SubtitleStyleViewModel.ForegroundSwatches[1]);

        Assert.Equal(SubtitleStyleViewModel.ForegroundSwatches[1], page.ForegroundHex);
    }

    [Fact]
    public void The_swatch_command_refuses_anything_that_is_not_a_colour()
    {
        var page = new SubtitleStyleViewModel(new InMemoryPreferences());
        var before = page.ForegroundHex;

        Assert.False(page.ApplyForegroundCommand.CanExecute("verde"));
        Assert.False(page.ApplyForegroundCommand.CanExecute(null));
        Assert.False(page.ApplyBackgroundCommand.CanExecute(42));
        page.ApplyForegroundCommand.Execute("verde");
        page.ApplyBackgroundCommand.Execute(null);

        Assert.Equal(before, page.ForegroundHex);
    }

    [Fact]
    public void The_grid_both_pickers_open_with_is_the_domains_own() =>
        Assert.Same(AccentPalette.Grid, SubtitleStyleViewModel.ColourGrid);

    private static bool[] ForegroundFlags(SubtitleStyleViewModel page) =>
    [
        page.IsFirstForeground,
        page.IsSecondForeground,
        page.IsThirdForeground,
        page.IsFourthForeground,
        page.IsFifthForeground,
        page.IsSixthForeground,
    ];

    private static bool[] BackgroundFlags(SubtitleStyleViewModel page) =>
    [
        page.IsFirstBackground,
        page.IsSecondBackground,
        page.IsThirdBackground,
        page.IsFourthBackground,
        page.IsFifthBackground,
        page.IsSixthBackground,
    ];

    private sealed class InMemoryPreferences : IPlaybackPreferenceRepository
    {
        private readonly Dictionary<(PreferenceScope, string), PlaybackPreference> _stored = [];

        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue((scope, scopeKey), out var value) ? value : null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preference);
            _stored[(preference.Scope, preference.ScopeKey)] = preference;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            _ = _stored.Remove((scope, scopeKey));
            return Task.CompletedTask;
        }
    }
}
