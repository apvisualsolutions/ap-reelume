// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows.Startup;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Startup;

/// <summary>
/// Which copy the recovery screen offers when the database will not open.
/// </summary>
/// <remarks>
/// This ran unmeasured for as long as it lived as a private method inside a 1,857-line composition
/// file: the only way to reach it was to make a database fail to open. The logic decides what a
/// person gets offered on the worst day their library has, and choosing the wrong copy silently
/// costs them everything between the two.
/// </remarks>
public sealed class DatabaseStartupTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "apreelume-startup-" + Guid.NewGuid().ToString("N"));

    public DatabaseStartupTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void The_migration_copy_wins_when_it_exists()
    {
        var database = Path.Combine(_root, "library.db");
        var migration = Path.Combine(_root, "library.db.pre-migration-0007.bak");
        File.WriteAllText(migration, "migration");
        var older = Path.Combine(_root, "library.db.pre-migration-0001.bak");
        File.WriteAllText(older, "older");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(migration, DatabaseStartup.FindLatestBackup(database, migration));
    }

    [Fact]
    public void Without_a_migration_copy_the_most_recent_pre_migration_file_is_offered()
    {
        var database = Path.Combine(_root, "library.db");
        var older = Path.Combine(_root, "library.db.pre-migration-0001.bak");
        var newer = Path.Combine(_root, "library.db.pre-migration-0009.bak");
        File.WriteAllText(older, "older");
        File.WriteAllText(newer, "newer");
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow);

        Assert.Equal(newer, DatabaseStartup.FindLatestBackup(database, migrationBackupPath: null));
    }

    /// <summary>
    /// A migration path that names a file which is not there is not a copy, and pretending otherwise
    /// would offer the person a restore that cannot happen.
    /// </summary>
    [Fact]
    public void A_migration_path_that_does_not_exist_falls_back_to_the_directory()
    {
        var database = Path.Combine(_root, "library.db");
        var present = Path.Combine(_root, "library.db.pre-migration-0003.bak");
        File.WriteAllText(present, "present");

        Assert.Equal(
            present,
            DatabaseStartup.FindLatestBackup(database, Path.Combine(_root, "gone.bak")));
    }

    [Fact]
    public void With_no_copy_at_all_the_answer_says_so_instead_of_naming_a_file()
    {
        var database = Path.Combine(_root, "library.db");

        var answer = DatabaseStartup.FindLatestBackup(database, migrationBackupPath: null);

        Assert.Equal(Path.Combine(_root, "no-pre-migration-copy-available"), answer);
        Assert.False(File.Exists(answer));
    }

    /// <summary>Backups of a different database are not this database's backups.</summary>
    [Fact]
    public void A_copy_belonging_to_another_database_is_not_offered()
    {
        var database = Path.Combine(_root, "library.db");
        File.WriteAllText(Path.Combine(_root, "other.db.pre-migration-0001.bak"), "other");

        Assert.Equal(
            Path.Combine(_root, "no-pre-migration-copy-available"),
            DatabaseStartup.FindLatestBackup(database, migrationBackupPath: null));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
