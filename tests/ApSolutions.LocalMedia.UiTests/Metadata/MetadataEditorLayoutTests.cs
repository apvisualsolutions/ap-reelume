// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Metadata;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Metadata;

/// <summary>
/// What the metadata editor shows: eight fields that say what they are, three messages that cannot
/// land on top of each other, and two of them told apart from the third by grammar.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for the three messages as blocks with a glyph — conflict and unidentified on
/// <c>WarningSurfaceBrush</c>, no provider answer as a neutral fact — and notes "the way they can
/// overlap today". <b>Measured: they cannot.</b> All three are assigned in one method from one
/// <c>result.Outcome</c>, so exactly one is ever true. The document flagged a risk that the code had
/// already ruled out; the rows are separated anyway, because the guarantee lives in a private method
/// and a shared row turns any future second writer into three messages painted on top of each other.
/// </para>
/// <para>
/// And measuring found what the row did not know: <b>eight text boxes with no visible label at all</b>.
/// Every one carried its words in <c>AutomationProperties.Name</c> and painted nothing, so a screen
/// reader heard "Original title" and anybody looking saw eight identical boxes. The strings had always
/// existed. Same defect as the scan spinner, eight times over.
/// </para>
/// </remarks>
public sealed class MetadataEditorLayoutTests
{
    /// <summary>
    /// Every field says what it is, out loud and on screen.
    /// </summary>
    /// <remarks>
    /// Asserted over every text box rather than over the eight that were wrong, because the ninth
    /// added would be wrong the same way. A text box has no content of its own to carry its label —
    /// unlike a checkbox, which is why the checkboxes beside these were always readable.
    /// </remarks>
    [AvaloniaFact]
    public void Every_field_paints_the_words_it_announces()
    {
        var (window, view) = Show();

        var painted = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToArray();

        var boxes = view.GetVisualDescendants().OfType<TextBox>().ToArray();
        Assert.True(boxes.Length >= 8, $"the editor has {boxes.Length} fields, so this is measuring the wrong view.");
        foreach (var box in boxes)
        {
            var announced = AutomationProperties.GetName(box);
            Assert.False(
                string.IsNullOrWhiteSpace(announced),
                "a field announces nothing, so neither eye nor ear can tell what it is.");
            Assert.Contains(announced!, painted, StringComparer.Ordinal);
        }

        window.Close();
    }

    /// <summary>
    /// The three messages never land on each other, whatever a future writer does.
    /// </summary>
    /// <remarks>
    /// Mounted with no data context, which leaves every <c>IsVisible</c> at its default and puts all
    /// three on screen at once — the one arrangement that can answer this. Geometry rather than row
    /// indices: what matters is whether they are drawn over each other, not how the grid was written.
    /// </remarks>
    [AvaloniaFact]
    public void The_three_messages_cannot_be_painted_on_top_of_each_other()
    {
        var (window, view) = Show();

        var blocks = MessageNames
            .Select(name => Assert.Single(
                view.GetVisualDescendants().OfType<Control>(),
                control => control.Name == name))
            .Select(control => Bounds(control, view))
            .ToArray();

        Assert.All(blocks, rect => Assert.True(rect.Height > 0, "a message measured nothing, so this proves nothing."));
        for (var first = 0; first < blocks.Length; first++)
        {
            for (var second = first + 1; second < blocks.Length; second++)
            {
                Assert.False(
                    blocks[first].Intersects(blocks[second]),
                    $"{MessageNames[first]} and {MessageNames[second]} overlap at "
                        + $"{blocks[first]} and {blocks[second]}.");
            }
        }

        window.Close();
    }

    /// <summary>
    /// Two of the three are warnings and the third is a fact, and the glyph is what says so.
    /// </summary>
    /// <remarks>
    /// A conflict and an unidentified title are both "what you asked for did not happen"; a provider
    /// with no answer right now is neither a failure nor anybody's fault. Telling the two apart by
    /// colour alone would not survive the high-contrast themes, so the warning surface arrives with a
    /// glyph and the fact arrives without one.
    /// </remarks>
    [AvaloniaFact]
    public void Two_of_the_messages_are_warnings_and_the_third_is_a_fact()
    {
        var (window, view) = Show();
        var warning = ThemeColour("WarningSurfaceBrush");

        foreach (var name in new[] { "ConflictMessage", "UnidentifiedMessage" })
        {
            var notice = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Name == name);
            Assert.Equal(warning, Assert.IsAssignableFrom<ISolidColorBrush>(notice.Background).Color);
            Assert.True(notice.BorderThickness.Top > 0, $"{name} has no border, so colour is its only signal.");
            Assert.Contains(notice.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "⚠");
        }

        var neutral = Assert.Single(
            view.GetVisualDescendants().OfType<Control>(),
            control => control.Name == "NoProviderAnswerMessage");
        Assert.DoesNotContain(
            neutral.GetVisualDescendants().OfType<Border>(),
            border => border.Background is ISolidColorBrush brush && brush.Color == warning);

        window.Close();
    }

    /// <summary>
    /// The editor is a section of the library page, and its actions wrap.
    /// </summary>
    /// <remarks>
    /// <c>LibraryView</c> owns the level one of that pane, so this owns a level two — the same shape
    /// the settings page ended up with once it was measured assembled. And the action row is a
    /// <c>WrapPanel</c>, which is the eighth time that reason has been written down here.
    /// </remarks>
    [AvaloniaFact]
    public void The_editor_titles_as_a_section_and_its_actions_wrap()
    {
        var (window, view) = Show();

        var heading = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => (int)AutomationProperties.GetHeadingLevel(block) > 0);
        Assert.Equal(2, (int)AutomationProperties.GetHeadingLevel(heading));
        Assert.Equal(Assert.IsType<double>(Resource("FontSizeSubtitle")), heading.FontSize);

        Assert.DoesNotContain(
            view.GetVisualDescendants().OfType<StackPanel>(),
            panel => panel.Orientation == Orientation.Horizontal
                && panel.GetVisualChildren().OfType<Button>().Count() > 1);

        window.Close();
    }

    /// <summary>The three the editor can show, by the name each block carries.</summary>
    private static readonly string[] MessageNames =
        ["ConflictMessage", "UnidentifiedMessage", "NoProviderAnswerMessage"];

    private static Rect Bounds(Control control, Visual root)
    {
        var origin = control.TranslatePoint(new Point(0, 0), root) ?? default;
        return new Rect(origin, control.Bounds.Size);
    }

    private static (Window Window, MetadataEditorView View) Show()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var view = new MetadataEditorView();
        var window = new Window { Width = 900, Height = 1200, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current!;
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme, so nothing can paint it.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static object Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
    }
}
