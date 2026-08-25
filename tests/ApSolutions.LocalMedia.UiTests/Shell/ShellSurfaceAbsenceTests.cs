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
