// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// One owner for the player window coordinator (ARQ-008). The audit found it registered in the
/// container and simultaneously built by hand in <c>ShellView</c>: two instances meant the
/// registered one held geometry nobody ever read, and whichever half a future change wired would
/// silently disagree with the other.
/// </summary>
public sealed class WindowCoordinatorOwnershipTests
{
    [Fact]
    public void The_shell_view_owns_the_window_coordinator_and_the_container_stays_out()
    {
        var composition = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs"));
        var shellView = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Shell",
            "ShellView.axaml.cs"));

        // The coordinator is per-view window state: the view that owns the mini window owns it,
        // and a container registration would only ever produce a second, unread instance.
        Assert.DoesNotContain("PlayerWindowCoordinator", composition, StringComparison.Ordinal);
        Assert.Contains("PlayerWindowCoordinator _windowCoordinator = new()", shellView, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
