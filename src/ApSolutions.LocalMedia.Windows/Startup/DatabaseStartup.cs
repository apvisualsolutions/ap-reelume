// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Presentation.Recovery;
using Avalonia.Controls.ApplicationLifetimes;

namespace ApSolutions.LocalMedia.Windows.Startup;

/// <summary>
/// What the application offers when the database will not open.
/// </summary>
/// <remarks>
/// This lived inside the composition file, where the only way to reach it was to make a real
/// database fail. It decides what a person is handed on the worst day their library has, so it is
/// worth being able to ask it questions directly (ARQ-006).
/// </remarks>
public static class DatabaseStartup
{
    /// <summary>The name the answer takes when there is no copy to offer.</summary>
    public const string NoCopyAvailable = "no-pre-migration-copy-available";

    /// <summary>
    /// Builds the recovery screen for a database that refused to open, already pointed at whichever
    /// copy has the best chance of being the one the person wants back.
    /// </summary>
    public static DatabaseRecoveryView CreateRecoveryView(
        string databasePath,
        string? migrationBackupPath,
        string failureDetail)
    {
        var backupPath = FindLatestBackup(databasePath, migrationBackupPath);
        return new DatabaseRecoveryView
        {
            DataContext = new DatabaseRecoveryViewModel(
                databasePath,
                backupPath,
                failureDetail,
                action => HandleRecoveryAction(action, backupPath)),
        };
    }

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

    /// <summary>
    /// Carries out what the person chose on the recovery screen: show them where the copy lives, or
    /// close the application. Restoring is theirs to do with the file in front of them.
    /// </summary>
    public static void HandleRecoveryAction(DatabaseRecoveryAction action, string backupPath)
    {
        if (action == DatabaseRecoveryAction.OpenBackupFolder)
        {
            var folder = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true,
                });
            }
        }
        else if (action == DatabaseRecoveryAction.Exit
            && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
