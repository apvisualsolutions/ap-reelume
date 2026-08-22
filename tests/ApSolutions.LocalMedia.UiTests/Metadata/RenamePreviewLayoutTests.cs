// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Metadata;
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
/// The rename preview: two paths a person compares before agreeing to touch their files.
/// </summary>
/// <remarks>
/// <para>
/// §4 asks for both paths in monospace and for the literal arrow to keep its accessible name. The
/// monospace matters for the reason §4 gives about the duplicate list — figures and segments line up
/// under each other, so the eye finds the difference instead of hunting for it.
/// </para>
/// <para>
/// <b>And the truncation was cutting off the half that matters.</b> Both paths trimmed with
/// <c>CharacterEllipsis</c>, which eats the end — and the end of a rename is the filename, which is
/// the only part that differs. Measured on 2026-08-22, this Avalonia offers
/// <c>PathSegmentEllipsis</c>, which elides middle segments and keeps both ends. §4 says the same
/// thing about the restore wizard's roots in its own words: paths are told apart by their end.
/// </para>
/// </remarks>
public sealed class RenamePreviewLayoutTests
{
    /// <summary>
    /// Both paths are fixed-width and lose their middle rather than their end.
    /// </summary>
    /// <remarks>
    /// The family is compared against the resolved token, not against a copy of its name: a test
    /// carrying "Consolas" would agree with itself the day the token changed.
    /// </remarks>
    [AvaloniaFact]
    public void Both_paths_are_fixed_width_and_lose_their_middle()
    {
        var (window, view) = Show();
        var mono = Assert.IsType<FontFamily>(Resource("FontFamilyMono"));

        var paths = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => (block.Text ?? string.Empty).Contains("Arrival", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, paths.Length);
        Assert.All(paths, block =>
        {
            Assert.Equal(mono.Name, block.FontFamily.Name);
            Assert.Equal(TextTrimming.PathSegmentEllipsis, block.TextTrimming);
        });

        window.Close();
    }

    /// <summary>
    /// The arrow says what it means to anybody who cannot see it.
    /// </summary>
    /// <remarks>
    /// It is a symbol and it stays one — the words go in the accessible name, which is the half a
    /// screen reader gets. Without it the row reads as two paths and a character.
    /// </remarks>
    [AvaloniaFact]
    public void The_arrow_between_the_paths_says_what_it_means()
    {
        var (window, view) = Show();

        var arrow = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "→");
        Assert.Equal(Resource("RenameSeparatorAccessibleName"), AutomationProperties.GetName(arrow));

        window.Close();
    }

    /// <summary>Both languages say it, and neither survived translation untranslated.</summary>
    [AvaloniaFact]
    public void The_arrows_words_are_written_in_both_languages()
    {
        var byLanguage = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var culture in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(culture));
            byLanguage[culture] = Assert.IsType<string>(Resource("RenameSeparatorAccessibleName"));
        }

        Assert.NotEqual(byLanguage["es-ES"], byLanguage["en-US"]);
    }

    /// <summary>
    /// The preview is a section of the library pane, and its actions wrap.
    /// </summary>
    [AvaloniaFact]
    public void The_preview_titles_as_a_section_and_its_actions_wrap()
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

    private static (Window Window, RenamePreviewView View) Show()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var plan = new RenamePlan(
            Guid.Parse("66660001-0000-4000-8000-000000000001"),
            @"R:\media",
            [new RenameOperation(1, @"R:\media\Arrival.mkv", @"R:\media\Arrival (2016).mkv")],
            []);
        var renamer = new StubRenamer();
        var view = new RenamePreviewView
        {
            DataContext = new RenamePreviewViewModel(plan, new ExecuteRename(renamer), new UndoRename(renamer)),
        };
        var window = new Window { Width = 900, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static object Resource(string key)
    {
        Assert.True(
            Avalonia.Application.Current!.TryFindResource(key, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
    }

    /// <summary>A renamer that is never asked to do anything: this file measures the preview.</summary>
    private sealed class StubRenamer : ISafeFileRenamer
    {
        public Task<RenameExecutionResult> ExecuteAsync(
            RenamePlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.Succeeded, plan));

        public Task<RenameExecutionResult> UndoAsync(
            RenamePlan plan,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenameExecutionResult(RenameExecutionOutcome.Succeeded, plan));

        public Task<IReadOnlyList<RenameAuditEntry>> GetAuditLogAsync(
            Guid planId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RenameAuditEntry>>([]);
    }
}
