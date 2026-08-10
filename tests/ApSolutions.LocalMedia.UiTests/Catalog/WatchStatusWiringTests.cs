// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Catalog;

/// <summary>
/// The watched toggle existed as a control and never as a behaviour (CNT-A01): the card built it
/// with a null handler, so a person's mark went nowhere, and the container carried a dead
/// <c>WatchStatusViewModel</c> registration shadowed by the <c>new</c> the card actually uses.
/// </summary>
public sealed class WatchStatusWiringTests
{
    [Fact]
    public void The_watched_toggle_hands_the_mark_to_the_use_case()
    {
        var composition = CompositionSource();

        Assert.Contains("onWatchStatusChanged:", composition, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<SetWatchStatus>", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void Clearing_an_override_recomputes_under_the_threshold_in_force()
    {
        var composition = CompositionSource();

        Assert.Contains(
            "GetRequiredService<ConfigureWatchedThreshold>",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_dead_watch_status_registration_is_gone()
    {
        // The card news its own WatchStatusViewModel with the handler; a container registration
        // nothing resolves would be the double ownership ARQ-008 already taught us to refuse.
        Assert.DoesNotContain(
            "AddTransient<WatchStatusViewModel>",
            CompositionSource(),
            StringComparison.Ordinal);
    }

    private static string CompositionSource()
    {
        return CompositionSourceText.Read();
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
