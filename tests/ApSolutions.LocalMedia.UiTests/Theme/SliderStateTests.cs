// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// A slider's job is to say where its value is, which it does with two lengths of different colour.
/// </summary>
/// <remarks>
/// Phase 2g, five uses: three in the subtitle style panel, the transport bar's position, and the
/// recommendation weight. Measured before writing anything — a slider paints from two
/// <c>Border[TrackBackground]</c> elements with the same name, the first being the part already
/// travelled and the second the part still to go, plus a <c>Thumb</c>. It owns 32 keys, of which the
/// tick bar's are unreachable: no view sets <c>TickPlacement</c>, so no tick is ever drawn.
/// <c>TickFrequency</c> is set on all five, but that only makes the value snap.
/// </remarks>
[Collection("ThemeVariant")]
public sealed class SliderStateTests
{
    private const double NonTextMinimum = 3.0;

    public static TheoryData<string> Themes() =>
        ["Light", "Dark", "HighContrastLight", "HighContrastDark"];

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_travelled_part_is_told_apart_from_the_part_still_to_go(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);

        var (travelled, remaining, _) = scene.Read(state: null);
        var ratio = Ratio(travelled, remaining, theme);

        // Measured before this existed: both halves came from Windows' blue and a translucent black,
        // and the blue was byte-identical in Light and in HighContrastDark — the system's accent, not
        // this application's, in any theme.
        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: the travelled part of a slider differs from the rest of the track by "
                + $"{ratio:F2}:1, so it cannot say where the value is.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void A_switched_off_slider_still_says_where_its_value_is(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);

        var (travelled, remaining, _) = scene.Read(":disabled");
        var ratio = Ratio(travelled, remaining, theme);

        // Measured before this existed: disabled painted both halves #FFCCCCCC, one single colour, so
        // a switched-off slider stopped saying anything at all. Being unavailable is not the same as
        // having no value, and the transport bar is disabled whenever nothing is playing.
        Assert.True(
            ratio >= NonTextMinimum,
            $"{themeName}: a disabled slider separates its two halves by {ratio:F2}:1, which is a "
                + "control that has forgotten its own value rather than one that cannot be moved.");
    }

    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void The_handle_can_be_seen_on_the_track_it_sits_on(string themeName)
    {
        var theme = Resolve(themeName);
        using var scene = new Scene(theme);
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");

        foreach (var state in new string?[] { null, ":pointerover", ":pressed" })
        {
            var (_, _, thumb) = scene.Read(state);
            var ratio = ThemeContrast.Ratio(ThemeContrast.Painted(thumb, surface), surface);

            Assert.True(
                ratio >= NonTextMinimum,
                $"{themeName}: the {state ?? "resting"} handle reads {ratio:F2}:1 against the surface "
                    + "behind it, and the handle is the part a person aims at.");
        }
    }

    [AvaloniaFact]
    public void High_contrast_paints_a_slider_differently_from_the_ordinary_themes()
    {
        foreach (var (ordinary, contrast) in new[]
        {
            ("Light", "HighContrastLight"),
            ("Dark", "HighContrastDark"),
        })
        {
            string a;
            using (var first = new Scene(Resolve(ordinary)))
            {
                a = Show(first.Read(state: null));
            }

            string b;
            using (var second = new Scene(Resolve(contrast)))
            {
                b = Show(second.Read(state: null));
            }

            Assert.True(a != b, $"{contrast} paints a slider exactly like {ordinary}: {a}.");
        }
    }

    private static double Ratio(IBrush? first, IBrush? second, ThemeVariant theme)
    {
        var surface = ThemeContrast.Token(theme, "ShellSurfaceBrush");
        return ThemeContrast.Ratio(
            ThemeContrast.Painted(first, surface),
            ThemeContrast.Painted(second, surface));
    }

    private static string Show((IBrush? Travelled, IBrush? Remaining, IBrush? Thumb) read) =>
        $"travelled={Describe(read.Travelled)} remaining={Describe(read.Remaining)} thumb={Describe(read.Thumb)}";

    private static string Describe(IBrush? brush) => brush switch
    {
        null => "null",
        ISolidColorBrush solid => $"{solid.Color}@{solid.Opacity:0.##}",
        _ => brush.GetType().Name,
    };

    private static ThemeVariant Resolve(string name) => name switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        "HighContrastLight" => Presentation.Theme.AppThemeVariants.HighContrastLight,
        "HighContrastDark" => Presentation.Theme.AppThemeVariants.HighContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private sealed class Scene : IDisposable
    {
        private readonly Window _window;
        private readonly Slider _slider;

        public Scene(ThemeVariant theme)
        {
            Avalonia.Application.Current!.RequestedThemeVariant = theme;
            _slider = new Slider { Width = 200, Minimum = 0, Maximum = 100, Value = 40 };
            _window = new Window { Width = 320, Height = 200, Content = _slider };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>
        /// The two halves of the track and the handle. Both halves are called
        /// <c>TrackBackground</c> — the first in the tree is the part already travelled — so they are
        /// told apart by order and not by name.
        /// </summary>
        public (IBrush? Travelled, IBrush? Remaining, IBrush? Thumb) Read(string? state)
        {
            foreach (var candidate in new[] { ":pointerover", ":pressed", ":disabled" })
            {
                ((IPseudoClasses)_slider.Classes).Set(candidate, candidate == state);
            }

            Dispatcher.UIThread.RunJobs();
            var track = _slider.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == "TrackBackground")
                .ToList();
            Assert.Equal(2, track.Count);
            var thumb = _slider.GetVisualDescendants().OfType<Thumb>().Single();
            return (track[0].Background, track[1].Background, thumb.Background);
        }

        public void Dispose()
        {
            _window.Close();
            Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        }
    }
}
