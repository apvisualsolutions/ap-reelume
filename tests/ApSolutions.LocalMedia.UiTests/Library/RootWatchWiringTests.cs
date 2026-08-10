// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// Continuous watching is what makes the library follow the disk instead of the other way round.
/// The deep audit found the whole slice registered and never resolved (LIB-002/003):
/// <c>RootWatchCoordinator</c>, the debounced watcher, and the fallback scheduler existed, were
/// tested, and never started — the application only ever scanned when a button was pressed.
/// </summary>
public sealed class RootWatchWiringTests
{
    [Fact]
    public void The_application_starts_watching_its_roots_when_the_window_appears()
    {
        var composition = CompositionSource();

        Assert.Contains(
            "GetRequiredService<RootWatchBackground>().Start()",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Leaving_the_application_stops_the_watchers_still_running()
    {
        var composition = CompositionSource();

        Assert.Contains(
            "GetRequiredService<RootWatchBackground>().Stop()",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_root_scanned_by_hand_is_watched_from_then_on()
    {
        var composition = CompositionSource();

        // Onboarding starts the first scan the moment a root is granted; ensuring the watch there
        // means a freshly added folder is followed without waiting for the next launch.
        Assert.Contains(".EnsureWatching(rootId)", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_scan_trigger_identifies_what_it_found_not_only_the_manual_one()
    {
        var composition = CompositionSource();

        // The identification hand-off lives inside the scan coordinator the whole application
        // shares, so a watcher-triggered scan feeds the review inbox exactly like a manual one.
        Assert.Contains(
            "AddSingleton<IScanCoordinator>(provider => new IdentifyingScanCoordinator(",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_fallback_scheduler_is_given_a_real_recovery_interval()
    {
        var composition = CompositionSource();

        // Without an interval the Continuous policy silently means "never": the scheduler yields
        // the startup pass and then nothing recovers a lost event for USB and NAS roots.
        Assert.Contains(
            "FallbackScanScheduler.DefaultRecoveryInterval",
            composition,
            StringComparison.Ordinal);
    }

    private static string CompositionSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the host keeps it.");
        return File.ReadAllText(path);
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
