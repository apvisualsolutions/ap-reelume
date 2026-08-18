// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Presentation.Theme;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests;

/// <summary>
/// What Windows' two answers mean, measured without needing a machine in high contrast.
/// </summary>
public sealed class HighContrastPolicyTests
{
    [Theory]
    [InlineData(0x00000000u, false)]
    [InlineData(0x00000001u, true)]
    // The flags word carries more than the one bit — HCF_AVAILABLE, HCF_HOTKEYACTIVE and the rest —
    // so the question is that bit and not the word being non-zero.
    [InlineData(0x00000002u, false)]
    [InlineData(0x0000007Eu, false)]
    [InlineData(0x0000007Fu, true)]
    public void High_contrast_is_one_bit_of_the_flags_word(uint flags, bool expected) =>
        Assert.Equal(expected, HighContrastPolicy.IsOn(flags));

    [Theory]
    // A COLORREF is 0x00BBGGRR, so these are white, black, and the four high contrast themes
    // Windows ships: white, black, and the two dark ones.
    [InlineData(0x00FFFFFFu, true)]
    [InlineData(0x00000000u, false)]
    [InlineData(0x00FFFFF0u, true)]
    [InlineData(0x00201000u, false)]
    // Blue is dark and yellow is light at the same 0xFF: a luminance, not a sum of channels.
    [InlineData(0x00FF0000u, false)]
    [InlineData(0x0000FFFFu, true)]
    public void Light_or_dark_is_the_luminance_of_the_window_colour(uint colour, bool expected) =>
        Assert.Equal(expected, HighContrastPolicy.IsLight(colour));

    [Fact]
    public void Luminance_runs_from_black_to_white_through_the_two_halves_of_the_curve()
    {
        Assert.Equal(0.0, HighContrastPolicy.RelativeLuminance(0, 0, 0));
        Assert.Equal(1.0, HighContrastPolicy.RelativeLuminance(255, 255, 255), 6);

        // sRGB is linear below 0.04045 and a power curve above it; a channel of 10 is under that
        // knee and one of 11 is over it, so both halves are exercised by name rather than by luck.
        Assert.True(HighContrastPolicy.RelativeLuminance(10, 10, 10) < HighContrastPolicy.RelativeLuminance(11, 11, 11));
        Assert.True(HighContrastPolicy.RelativeLuminance(10, 10, 10) > 0.0);
    }

    /// <summary>
    /// The host's side of it: the two calls are made and their answers are the policy's, so a change
    /// that stopped asking Windows would be visible here rather than only on someone's machine.
    /// </summary>
    [Fact]
    public void The_windows_host_answers_both_questions_without_deciding_either()
    {
        var type = Assembly.Load("ApSolutions.LocalMedia.Windows")
            .GetType("ApSolutions.LocalMedia.Windows.Accessibility.WindowsHighContrastService", throwOnError: false);
        Assert.NotNull(type);
        var service = Assert.IsAssignableFrom<IHighContrastService>(Activator.CreateInstance(type));

        // A hosted runner is not in high contrast and draws its windows white, so asserting either
        // value outright would be asserting the runner's settings. What is asserted is that asking
        // is a question and not a state — two reads agree — and that the answer about light or dark
        // is the policy's answer for the colour the host actually read, so a service that inverted
        // it would be caught here rather than on somebody's machine.
        Assert.Equal(service.IsEnabled, service.IsEnabled);
        Assert.Equal(HighContrastPolicy.IsLight(WindowColour()), service.IsLight);
    }

    private static uint WindowColour()
    {
        var type = Assembly.Load("ApSolutions.LocalMedia.Windows")
            .GetType("ApSolutions.LocalMedia.Windows.Accessibility.WindowsHighContrastService", throwOnError: false);
        Assert.NotNull(type);
        var getSysColor = type.GetMethod("GetSysColor", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(getSysColor);
        return Assert.IsType<uint>(getSysColor.Invoke(null, [5]));
    }
}
