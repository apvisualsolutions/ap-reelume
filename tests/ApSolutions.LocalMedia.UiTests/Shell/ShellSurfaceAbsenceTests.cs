// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// What the shell answers for a surface the composition did not hand it, and what the rail's badge
/// says once somebody counts the inbox.
/// </summary>
/// <remarks>
/// A shell built with no surfaces is not a hypothetical: four suites mount one, and the recovery
/// screen stands in the shell's place with none of them. Every entry point has to return rather than
/// reach through a null — and the badge has to be readable, which is the half the count itself
/// cannot assert.
/// </remarks>
public sealed class ShellSurfaceAbsenceTests
{
    [Fact]
    public async Task An_entry_point_with_no_surface_behind_it_returns_instead_of_reaching_through()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        await shell.OpenLoosePlayerAsync("C:\\media\\loose.mkv", TestContext.Current.CancellationToken);

        Assert.Null(shell.Player);
        Assert.False(shell.HasLooseFile);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            shell.OpenLoosePlayerAsync(" ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_shell_refuses_to_exist_without_a_way_to_navigate_or_anything_to_show()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ShellViewModel(null!, new ShellSurfaces()));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new ShellViewModel(new NavigationService(), (ShellSurfaces)null!));
    }

    /// <summary>
    /// The two commands that take a value refuse anything that is not one of their own.
    /// </summary>
    /// <remarks>
    /// Both are pressed with a parameter the markup supplies, so a wrong one can only arrive from a
    /// caller — and a caller that gets it wrong has to be told rather than obeyed. There is no state
    /// to check afterwards, which is the point: nothing happened.
    /// </remarks>
    [Fact]
    public void A_command_that_takes_a_value_refuses_one_that_is_not_its_own()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        var before = shell.CurrentRoute;

        shell.NavigateCommand.Execute("Biblioteca");
        shell.NavigateCommand.Execute(null);
        shell.TogglePlayerPanelCommand.Execute("Audio");
        shell.TogglePlayerPanelCommand.Execute(7);

        Assert.Equal(before, shell.CurrentRoute);
        Assert.Equal(PlayerPanel.None, shell.PlayerPanel);
    }

    /// <summary>
    /// A tray with more than the page it reads says so with a mark rather than with a number.
    /// </summary>
    /// <remarks>
    /// The count comes from a query with a ceiling of a hundred, so a hundred and one means «more
    /// than this page holds» rather than a hundred and one things. Printing it would be a number the
    /// application made up.
    /// </remarks>
    [Fact]
    public void A_tray_past_the_page_it_reads_says_so_instead_of_inventing_a_number()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        shell.ApplyReviewPendingCount(7);
        var few = shell.ReviewPendingText;
        shell.ApplyReviewPendingCount(101);
        var many = shell.ReviewPendingText;

        Assert.Equal("7", few);
        Assert.NotEqual(few, many);
        Assert.DoesNotContain("101", many, StringComparison.Ordinal);
    }

    /// <summary>
    /// The header's own actions answer for a shell that was handed no surfaces to act on.
    /// </summary>
    [Fact]
    public void The_actions_a_shell_offers_are_the_ones_it_was_given_something_to_do_with()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());

        Assert.False(shell.AddMediaCommand.CanExecute(null));
        Assert.False(shell.EditMetadataCommand.CanExecute(null));
        Assert.False(shell.ToggleMiniPlayerCommand.CanExecute(null));
        Assert.False(shell.HasPlayerPanels);

        // Pressing them anyway does nothing rather than reaching through a surface nobody handed it.
        shell.AddMediaCommand.Execute(null);
        shell.ToggleMiniPlayerCommand.Execute(null);
        Assert.False(shell.IsAddingRoot);
    }

    [Fact]
    public void The_rails_badge_reads_back_what_was_counted_and_never_goes_negative()
    {
        var shell = new ShellViewModel(new NavigationService(), new ShellSurfaces());
        var announced = new List<string>();
        shell.PropertyChanged += (_, args) => announced.Add(args.PropertyName ?? string.Empty);

        shell.ApplyReviewPendingCount(4);
        Assert.Equal(4, shell.ReviewPendingCount);
        Assert.True(shell.HasReviewPending);
        Assert.Contains(nameof(shell.HasReviewPending), announced);

        // The same count again says nothing, which is what keeps a repeated read off the interface
        // thread's queue.
        announced.Clear();
        shell.ApplyReviewPendingCount(4);
        Assert.Empty(announced);

        shell.ApplyReviewPendingCount(-2);
        Assert.Equal(0, shell.ReviewPendingCount);
        Assert.False(shell.HasReviewPending);
    }
}
