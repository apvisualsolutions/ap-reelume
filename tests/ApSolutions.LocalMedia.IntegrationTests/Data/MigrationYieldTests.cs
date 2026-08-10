// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Infrastructure.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Data;

/// <summary>
/// ARQ-005: what <c>MigrateAsync</c> actually does to the thread that calls it.
/// </summary>
/// <remarks>
/// It is written with real awaits throughout, which is not the same as yielding.
/// <c>Microsoft.Data.Sqlite</c> implements most of its <c>Async</c> surface synchronously, because
/// SQLite has no asynchronous I/O to defer to; every one of those awaits therefore completes without
/// ever leaving the caller. This matters at exactly one place — the startup path, which runs on the
/// interface thread — because it decides between two corrections that look identical in a diff:
/// awaiting the call, which changes nothing at all here, and moving the work off the thread. This
/// suite is what makes that decision from a measurement instead of from the shape of the source.
/// </remarks>
public sealed class MigrationYieldTests(ITestOutputHelper output)
{
    /// <summary>
    /// The measurement the correction rests on, taken against the real migrations on a new database
    /// — which is the first launch, the slowest case, and the one the interface thread pays for.
    /// </summary>
    /// <remarks>
    /// A task that is already complete when the call returns cannot have yielded: there was no
    /// suspension point, so every await resumed inline and the whole migration ran on the caller.
    /// Should SQLite's provider ever gain real asynchronous I/O, this fails — and the failure is the
    /// notice that the work no longer needs a thread of its own.
    /// </remarks>
    [Fact]
    public async Task Migrating_a_new_database_never_yields_the_thread_that_asked_for_it()
    {
        using var directory = new DatabaseTestDirectory();
        using var runner = new MigrationRunner(new SqliteConnectionFactory(directory.DatabasePath));

        var elapsed = Stopwatch.StartNew();
        var migrating = runner.MigrateAsync(TestContext.Current.CancellationToken);
        var heldForMilliseconds = elapsed.Elapsed.TotalMilliseconds;
        var finishedBeforeReturning = migrating.IsCompleted;
        await migrating;
        var totalMilliseconds = elapsed.Elapsed.TotalMilliseconds;

        output.WriteLine(
            $"{runner.AppliedMigrationCount} migration(s): the call held the thread for "
                + $"{heldForMilliseconds:F0} ms of the {totalMilliseconds:F0} ms it took in total.");

        Assert.True(runner.AppliedMigrationCount > 0, "Nothing was migrated, so nothing was measured.");
        Assert.True(
            finishedBeforeReturning,
            "MigrateAsync yielded, so awaiting it would be enough to free the interface thread and "
                + "the startup no longer needs a thread of its own.");
    }
}
