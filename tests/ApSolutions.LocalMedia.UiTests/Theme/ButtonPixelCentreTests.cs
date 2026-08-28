// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Runtime.InteropServices;

using ApSolutions.LocalMedia.Presentation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// Where the ink actually lands on screen, counted in pixels rather than derived from a box.
/// </summary>
/// <remarks>
/// <para>
/// This repository already had two gates about this and <b>both were green over two pixels of
/// visible misalignment</b>. <c>ButtonInkTests</c> measures the label's box; <c>ButtonOpticalCentreTests</c>
/// measures the run of ink computed from the font's own metrics. Neither renders anything, and the
/// defect lived only in what was rendered: a five pixel margin on the label, calibrated against a
/// 2.43 px asymmetry the metrics reported, moved the word three pixels on screen — from one low to
/// two high — and dragged the icon beside it off centre on the way.
/// </para>
/// <para>
/// That is a new shape of this repository's characteristic defect. Not a service nobody resolves,
/// but <b>a gate that measures the model of the thing it promises to watch</b>. The answer is to
/// rasterise: <c>CaptureRenderedFrame</c> works here because <c>TestAppBuilder</c> brings up real
/// Skia with <c>UseHeadlessDrawing = false</c>, and what it returns is what a person sees.
/// </para>
/// <para>
/// <b>The words are parameters and at least one of them carries a descender.</b> The middle of a
/// word's ink is not a property of the font, it is a property of the string: measured in Inter at
/// 14 px, «Guardar» sits +0.70 from the middle of its line box and «Reproducir» +2.23, a range of
/// 3.2 px that opens and closes with a single letter — and moves again when the interface is
/// translated. A gate that pinned one word would certify a compensation calibrated for that word.
/// </para>
/// </remarks>
public sealed class ButtonPixelCentreTests
{
    /// <summary>The white the scene is painted on; anything darker than this is a control.</summary>
    private const int Paper = 250;

    /// <summary>Ink is what is darker than this; the button's own fill sits between the two.</summary>
    private const int Ink = 110;

    [AvaloniaTheory]
    [InlineData("es-ES", "Guardar")]
    [InlineData("es-ES", "Reproducir")]
    [InlineData("es-ES", "Añadir medios…")]
    [InlineData("en-US", "Save")]
    [InlineData("en-US", "Save the report")]
    public void The_icon_and_the_word_of_a_button_land_on_the_same_pixel(string language, string word)
    {
        using var scene = new Scene(language, word);

        Assert.True(
            Math.Abs(scene.IconMiddle - scene.WordMiddle) <= 1.0,
            $"{language} draws «{word}» with its icon centred on row {scene.IconMiddle:0.0} and its "
                + $"word on row {scene.WordMiddle:0.0} — {scene.IconMiddle - scene.WordMiddle:+0.0;-0.0} px "
                + "apart on screen. They sit side by side in one button and a reader compares them "
                + "against each other.");
    }

    /// <summary>
    /// And the pair sits in the middle of the button, which is the half the five pixel margin was
    /// added for and overshot.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("es-ES", "Guardar")]
    [InlineData("es-ES", "Reproducir")]
    [InlineData("en-US", "Save the report")]
    public void The_word_of_a_button_lands_in_the_middle_of_it(string language, string word)
    {
        using var scene = new Scene(language, word);

        Assert.True(
            Math.Abs(scene.WordMiddle - scene.ButtonMiddle) <= 1.0,
            $"{language} draws «{word}» centred on row {scene.WordMiddle:0.0} inside a button whose "
                + $"middle is row {scene.ButtonMiddle:0.0}, so it reads "
                + $"{Math.Abs(scene.WordMiddle - scene.ButtonMiddle):0.0} px "
                + $"{(scene.WordMiddle > scene.ButtonMiddle ? "low" : "high")}.");
    }

    /// <summary>
    /// And the icon sits in the middle of the button, which is the third thing the retired gate
    /// asserted and the one that was already true.
    /// </summary>
    /// <remarks>
    /// It is carried over rather than dropped because it is the half that fails in the other
    /// direction: a compensation applied to the button's content moves the icon too, so a number
    /// chosen to put the word right can take the icon off centre without anything saying so. That is
    /// exactly what the five pixel margin did, in reverse.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData("es-ES", "Guardar")]
    [InlineData("en-US", "Save the report")]
    public void The_icon_of_a_button_lands_in_the_middle_of_it(string language, string word)
    {
        using var scene = new Scene(language, word);

        Assert.True(
            Math.Abs(scene.IconMiddle - scene.ButtonMiddle) <= 1.0,
            $"{language} draws the icon beside «{word}» centred on row {scene.IconMiddle:0.0} inside "
                + $"a button whose middle is row {scene.ButtonMiddle:0.0}.");
    }

    /// <summary>
    /// The scan finds ink at all, which is what keeps a silent pass from looking like a green one.
    /// </summary>
    /// <remarks>
    /// Every assertion above is a difference between two numbers, and two numbers that are both -1
    /// agree perfectly. A threshold that stopped matching, a frame that came back blank, or a button
    /// that never got a theme would all pass the two gates above by measuring nothing.
    /// </remarks>
    [AvaloniaFact]
    public void The_scan_finds_a_button_an_icon_and_a_word_before_it_compares_them()
    {
        using var scene = new Scene("es-ES", "Guardar");

        Assert.True(scene.ButtonTop >= 0 && scene.ButtonBottom > scene.ButtonTop, "no button was rasterised.");
        Assert.True(scene.IconTop >= 0 && scene.IconBottom > scene.IconTop, "no icon ink was found.");
        Assert.True(scene.WordTop >= 0 && scene.WordBottom > scene.WordTop, "no word ink was found.");

        // The icon is 12 px tall and the word's ink is shorter than its line box: numbers well away
        // from these would mean the bands are reading each other rather than their own control.
        Assert.InRange(scene.IconBottom - scene.IconTop, 8, 16);
        Assert.InRange(scene.WordBottom - scene.WordTop, 6, 18);
    }

    /// <summary>One rasterised button, and the rows its parts landed on.</summary>
    /// <remarks>
    /// The icon is a plain square rather than one of the thirty-five from the dictionary, and that is
    /// deliberate: a square's ink fills its box exactly, so what this measures is the compensation
    /// and not a geometry that is off centre inside its own box for its own reasons. The columns are
    /// fixed because the scene is: 12 px of icon after the border and the padding, then the word.
    /// </remarks>
    private sealed class Scene : IDisposable
    {
        private readonly Window _window;

        public Scene(string language, string word)
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(language));

            var icon = new Avalonia.Controls.Shapes.Path
            {
                Width = 12,
                Height = 12,
                Fill = Brushes.Black,
                Data = Geometry.Parse("M 0,0 L 12,0 L 12,12 L 0,12 Z"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = new TextBlock
            {
                Text = word,
                Foreground = Brushes.Black,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { icon, label },
            };
            var button = new Button { Content = row, Height = 44, Padding = new Thickness(12, 0) };
            var host = new Border
            {
                Background = Brushes.White,
                Child = button,
                Padding = new Thickness(10),
            };

            _window = new Window { Width = 300, Height = 80, Content = host };
            _window.Show();
            Dispatcher.UIThread.RunJobs();

            using var frame = _window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException("the headless backend returned no frame.");
            using var buffer = frame.Lock();
            var size = frame.PixelSize;

            ButtonTop = Scan(buffer, size, 12, size.Width - 12, Paper, out var buttonBottom);
            ButtonBottom = buttonBottom;
            IconTop = Scan(buffer, size, 20, 38, Ink, out var iconBottom);
            IconBottom = iconBottom;
            WordTop = Scan(buffer, size, 42, size.Width - 20, Ink, out var wordBottom);
            WordBottom = wordBottom;
        }

        public int ButtonTop { get; }

        public int ButtonBottom { get; }

        public int IconTop { get; }

        public int IconBottom { get; }

        public int WordTop { get; }

        public int WordBottom { get; }

        public double ButtonMiddle => (ButtonTop + ButtonBottom) / 2.0;

        public double IconMiddle => (IconTop + IconBottom) / 2.0;

        public double WordMiddle => (WordTop + WordBottom) / 2.0;

        public void Dispose() => _window.Close();

        /// <summary>The first and last row carrying a pixel darker than <paramref name="threshold"/>.</summary>
        private static int Scan(
            ILockedFramebuffer buffer,
            PixelSize size,
            int fromX,
            int toX,
            int threshold,
            out int bottom)
        {
            var bytes = new byte[buffer.RowBytes * size.Height];
            Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

            var top = -1;
            bottom = -1;
            for (var y = 0; y < size.Height; y++)
            {
                for (var x = fromX; x < Math.Min(toX, size.Width); x++)
                {
                    var i = (y * buffer.RowBytes) + (x * 4);
                    if (bytes[i] >= threshold || bytes[i + 1] >= threshold || bytes[i + 2] >= threshold)
                    {
                        continue;
                    }

                    if (top < 0)
                    {
                        top = y;
                    }

                    bottom = y;
                    break;
                }
            }

            return top;
        }
    }
}
