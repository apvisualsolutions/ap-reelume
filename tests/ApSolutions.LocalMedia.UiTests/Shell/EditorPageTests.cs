// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// The two title tools are a page of their own, the way the prototype draws them.
/// </summary>
/// <remarks>
/// <para>
/// They were a <c>TabControl</c> at the bottom of the library's own scroll until 2026-08-28, so
/// opening one put a panel under a whole grid of cards — which is why the walk needed a 2,000 px
/// window to reach it at all. The prototype draws a page with «Volver · Biblioteca» on top, a header,
/// and two pills.
/// </para>
/// <para>
/// Every case here is an <c>AvaloniaFact</c> and not a <c>Fact</c>, including the four that assert
/// only properties: they pump the dispatcher, and <c>RunJobs</c> called off the UI thread is a red
/// that lands on whatever test ran next. It cost one — <c>MarkerUiTests</c> failed beside these two
/// on the first full run and passed alone on the second.
/// </para>
/// <para>
/// It is deliberately <b>not</b> a sixth <c>AppRoute</c>: the five approved destinations are
/// asserted by name in <see cref="ShellAssemblyTests"/> and the walk reaches each of them by its rail
/// button, so a sixth value would break the first and leave the walk navigating to a place with no
/// door. It covers the library's slot instead, the way a playing session covers whatever route is
/// underneath it.
/// </para>
/// </remarks>
public sealed class EditorPageTests
{
    [AvaloniaFact]
    public async Task Opening_a_tool_covers_the_library_and_closing_puts_it_back()
    {
        var shell = Shell();
        Assert.True(shell.IsLibraryListVisible);
        Assert.False(shell.IsEditorVisible);

        await OpenEditorAsync(shell);

        Assert.True(shell.IsEditorVisible);
        Assert.False(shell.IsLibraryListVisible);

        shell.CloseEditorCommand.Execute(null);

        Assert.False(shell.IsEditorVisible);
        Assert.True(shell.IsLibraryListVisible);
        Assert.False(shell.HasMetadataEditor);
        Assert.False(shell.HasRename);
    }

    /// <summary>
    /// Neither page is drawn on another destination, and that is measured rather than assumed.
    /// </summary>
    /// <remarks>
    /// The first draft bound the library's list to <c>!HasEditorPanel</c> alone, which drew the whole
    /// library on top of Settings and every other destination because nothing was left saying which
    /// route this is. <c>ThemeTests</c> caught it by counting sixteen appearance buttons where
    /// thirteen exist — this asserts it directly, where it can be read.
    /// </remarks>
    [AvaloniaFact]
    public async Task Neither_the_list_nor_the_editor_is_drawn_on_another_destination()
    {
        var shell = Shell();
        await OpenEditorAsync(shell);
        Assert.True(shell.IsEditorVisible);

        shell.NavigateCommand.Execute(AppRoute.Settings);

        Assert.False(shell.IsEditorVisible);
        Assert.False(shell.IsLibraryListVisible);

        // And it is still open underneath: coming back to the library finds the tool where it was
        // left, which is what a page covering a route means rather than one replacing it.
        shell.NavigateCommand.Execute(AppRoute.Library);
        Assert.True(shell.IsEditorVisible);
        Assert.True(shell.HasMetadataEditor);
    }

    /// <summary>
    /// A pill opens its tool as well as selecting it, which is what keeps the page from being a dead
    /// end.
    /// </summary>
    /// <remarks>
    /// Measured by the walk the day the page landed: with the page covering the card,
    /// «Previsualizar renombrado» was no longer on screen — it matched zero controls — so somebody
    /// who opened the metadata editor had no way left to reach the other tool. The prototype draws
    /// both pills always, and each one opens.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_other_pill_opens_the_tool_it_names_rather_than_offering_a_blank_page()
    {
        var shell = Shell();
        await OpenEditorAsync(shell);
        Assert.True(shell.IsMetadataTab);
        Assert.False(shell.HasRename);

        shell.ShowRenameTabCommand.Execute(null);
        await Task.Yield();

        Assert.True(shell.HasRename);
        Assert.True(shell.IsRenameTab);
        Assert.True(shell.IsRenameTabOpen);
        Assert.False(shell.IsMetadataTabOpen);

        // And back, which selects rather than opens because the editor is already there.
        shell.ShowMetadataTabCommand.Execute(null);
        await Task.Yield();

        Assert.True(shell.IsMetadataTabOpen);
        Assert.False(shell.IsRenameTabOpen);
        Assert.True(shell.HasRename);
    }

    /// <summary>
    /// The header names the title the page was opened for, and not what is being typed into it.
    /// </summary>
    /// <remarks>
    /// Bound to the editor's own title field, a header would rewrite itself letter by letter while
    /// somebody edited the name — and the rename preview has no title of its own at all, while this
    /// one page serves both pills.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_header_names_the_card_it_was_opened_from()
    {
        var shell = Shell();
        var expected = shell.Library!.Items[0].Item.Title;

        await OpenEditorAsync(shell);

        Assert.Equal(expected, shell.EditorTitle);

        shell.MetadataEditor!.Title = "algo que alguien está escribiendo";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(expected, shell.EditorTitle);
    }

    /// <summary>
    /// The page's three commands answer to the selection, the way the card's three tools do.
    /// </summary>
    /// <remarks>
    /// They were left out of the library's own change handler when the page landed, which is the
    /// quiet half of this shape: the three older commands were refreshed there and the three new ones
    /// rest on exactly the same <c>SelectedTitleId</c>. A pill that opens a tool for "the open title"
    /// cannot be the last to hear that a different title is open.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_pages_commands_answer_to_the_selection_like_the_cards_do()
    {
        var shell = Shell();
        var library = shell.Library!;

        Assert.Null(library.SelectedItem);
        Assert.False(shell.ShowMetadataTabCommand.CanExecute(null));
        Assert.False(shell.ShowRenameTabCommand.CanExecute(null));
        Assert.Equal(string.Empty, shell.EditorTitle);

        var raised = new List<string>();
        shell.PropertyChanged += (_, args) => raised.Add(args.PropertyName ?? string.Empty);

        // The event and not only the answer, and that distinction is the whole test: CanExecute
        // evaluates its predicate on every call, so it would read true after the selection with or
        // without the fix. What a button on screen listens to is CanExecuteChanged, and nothing was
        // raising it for these three.
        var metadataTold = 0;
        var renameTold = 0;
        var closeTold = 0;
        shell.ShowMetadataTabCommand.CanExecuteChanged += (_, _) => metadataTold++;
        shell.ShowRenameTabCommand.CanExecuteChanged += (_, _) => renameTold++;
        shell.CloseEditorCommand.CanExecuteChanged += (_, _) => closeTold++;

        await library.OpenDetailsAsync(library.Items[0], TestContext.Current.CancellationToken);

        // At least once each rather than an exact total: opening a card moves both SelectedItem and
        // Surface, so the handler runs twice, and a count would be asserting how many properties the
        // library happens to change. Without the fix all three are zero.
        Assert.True(metadataTold >= 1, "the metadata pill was never told the selection changed.");
        Assert.True(renameTold >= 1, "the renaming pill was never told the selection changed.");
        Assert.True(closeTold >= 1, "Back was never told the selection changed.");
        Assert.True(shell.ShowMetadataTabCommand.CanExecute(null));
        Assert.True(shell.ShowRenameTabCommand.CanExecute(null));
        Assert.Contains(nameof(ShellViewModel.EditorTitle), raised);
        Assert.Equal(library.Items[0].Item.Title, shell.EditorTitle);
    }

    /// <summary>Both pills are on screen, and the one in force is the one that reads as pressed.</summary>
    [AvaloniaFact]
    public async Task The_page_draws_its_way_back_and_both_of_its_pills()
    {
        var shell = Shell();
        await OpenEditorAsync(shell);
        var view = new ShellView { DataContext = shell };
        var window = new Window { Width = 1280, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var back = view.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "EditorBackAction");
        var pills = view.GetVisualDescendants()
            .OfType<ToggleButton>()
            .Where(toggle => toggle.Name is "EditorMetadataTab" or "EditorRenameTab")
            .ToArray();

        Assert.True(back.IsEffectivelyVisible);
        Assert.Equal(2, pills.Length);
        Assert.All(pills, pill => Assert.True(pill.IsEffectivelyVisible));
        Assert.All(pills, pill => Assert.Contains("segment", pill.Classes));

        // Exactly one reads as pressed: two pills both saying "this one" is the state the prototype's
        // aria-pressed exists to make impossible.
        Assert.Single(pills, pill => pill.IsChecked == true);
        Assert.Equal("EditorMetadataTab", pills.Single(pill => pill.IsChecked == true).Name);
        window.Close();
    }

    private static async Task OpenEditorAsync(ShellViewModel shell)
    {
        await shell.Library!.OpenDetailsAsync(shell.Library.Items[0], TestContext.Current.CancellationToken);
        await shell.OpenMetadataEditorAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
    }

    private static ShellViewModel Shell()
    {
        var navigation = new NavigationService();
        var shell = new ShellViewModel(navigation, ShellAssemblyTests.EditorSurfaces());
        navigation.Navigate(AppRoute.Library);
        return shell;
    }
}
