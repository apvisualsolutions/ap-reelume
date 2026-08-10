// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Windows.Startup;

/// <summary>
/// Which copy the application offers when the database will not open.
/// </summary>
/// <remarks>
/// Only the decision lives here, not the screen that shows it or the action it carries out: those
/// need a window and a shell, and a class that cannot be exercised without them would arrive with
/// the coverage it could not have. The decision is the part worth being able to ask questions of
/// (ARQ-006), and it now has them.
/// </remarks>
public static class DatabaseStartup
{
    /// <summary>The name the answer takes when there is no copy to offer.</summary>
    public const string NoCopyAvailable = "no-pre-migration-copy-available";

    /// <summary>
    /// The copy to offer: the one the migration just took if it is really there, otherwise the most
    /// recent pre-migration copy of <em>this</em> database.
    /// </summary>
    /// <remarks>
    /// A migration path naming a file that is not on disk is not a copy, and offering it would
    /// promise a restore that cannot happen — so it falls back rather than being trusted. When
    /// nothing is found the answer is a path that deliberately does not exist, which the recovery
    /// screen reads as "there is nothing to restore from" instead of naming a file.
    /// </remarks>
    public static string FindLatestBackup(string databasePath, string? migrationBackupPath)
    {
        if (!string.IsNullOrWhiteSpace(migrationBackupPath) && File.Exists(migrationBackupPath))
        {
            return migrationBackupPath;
        }

        var directory = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("The database path has no directory.");
        var pattern = $"{Path.GetFileName(databasePath)}.pre-migration-*.bak";
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, pattern)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault()
                ?? Path.Combine(directory, NoCopyAvailable)
            : Path.Combine(directory, NoCopyAvailable);
    }
}
