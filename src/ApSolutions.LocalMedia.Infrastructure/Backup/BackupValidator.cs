// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Backup;

namespace ApSolutions.LocalMedia.Infrastructure.Backup;

/// <summary>
/// Reads an incoming archive with the assumption that it may be hostile. Nothing is written anywhere:
/// entries are read into memory one at a time, bounded, and every one of them has to be on the allowlist
/// and stay inside the folder it claims to belong to.
/// <para>
/// The entry list this returns holds only what passed. An attacker's entry is named in a finding, never
/// in the list of things a later step might unpack.
/// </para>
/// </summary>
public sealed class BackupValidator : IBackupValidator
{
    /// <summary>Generous for a poster and a database, and far below anything that could exhaust a disk.</summary>
    public const long MaximumEntryBytes = 512L * 1024 * 1024;

    public const long MaximumTotalBytes = 2L * 1024 * 1024 * 1024;

    public async Task<RestoreInspection> InspectAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        var findings = new List<RestoreFinding>();
        var accepted = new List<string>();
        var contents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        long uncompressed = 0;

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsEscape(entry.FullName))
                {
                    findings.Add(new RestoreFinding(RestoreFindingKind.PathEscape, entry.FullName));
                    continue;
                }

                if (!BackupContentPolicy.IsAllowed(entry.FullName))
                {
                    findings.Add(new RestoreFinding(RestoreFindingKind.ForbiddenEntry, entry.FullName));
                    continue;
                }

                if (entry.Length > MaximumEntryBytes)
                {
                    findings.Add(new RestoreFinding(RestoreFindingKind.EntryTooLarge, entry.FullName));
                    continue;
                }

                uncompressed += entry.Length;
                if (uncompressed > MaximumTotalBytes)
                {
                    findings.Add(new RestoreFinding(RestoreFindingKind.EntryTooLarge, entry.FullName));
                    break;
                }

                accepted.Add(entry.FullName);
                contents[entry.FullName] = await ReadAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new RestoreInspection(
                null,
                [new RestoreFinding(RestoreFindingKind.UnreadableArchive, exception.Message)],
                [],
                0);
        }

        var manifest = ReadManifest(contents, findings);
        if (manifest is null)
        {
            return new RestoreInspection(null, findings, accepted, uncompressed);
        }

        VerifyPromises(manifest, contents, findings);
        return new RestoreInspection(manifest, findings, accepted, uncompressed);
    }

    private static async Task<byte[]> ReadAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    /// <summary>
    /// True for anything that would land outside the folder it is unpacked into: a rooted path, a drive
    /// letter, or a relative segment that climbs. Checked on the name, before anything opens it.
    /// </summary>
    private static bool IsEscape(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return true;
        }

        var normalized = entryName.Replace('\\', '/');
        return normalized.StartsWith('/')
            || Path.IsPathRooted(normalized)
            || normalized.Split('/').Any(segment => segment == "..");
    }

    private static BackupManifest? ReadManifest(
        Dictionary<string, byte[]> contents,
        List<RestoreFinding> findings)
    {
        if (!contents.TryGetValue(BackupManifest.FileName, out var bytes))
        {
            findings.Add(new RestoreFinding(RestoreFindingKind.MissingEntry, BackupManifest.FileName));
            return null;
        }

        BackupManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(bytes, BackupSerialization.Options);
        }
        catch (JsonException exception)
        {
            findings.Add(new RestoreFinding(RestoreFindingKind.UnsupportedFormat, exception.Message));
            return null;
        }

        if (manifest is null)
        {
            findings.Add(new RestoreFinding(RestoreFindingKind.UnsupportedFormat, BackupManifest.FileName));
            return null;
        }

        if (manifest.FormatVersion != BackupManifest.CurrentFormatVersion)
        {
            findings.Add(new RestoreFinding(
                RestoreFindingKind.UnsupportedFormat,
                $"format {manifest.FormatVersion}"));
        }

        return manifest;
    }

    /// <summary>Checks that every file the manifest promised is present and unchanged.</summary>
    private static void VerifyPromises(
        BackupManifest manifest,
        Dictionary<string, byte[]> contents,
        List<RestoreFinding> findings)
    {
        Verify(BackupContentPolicy.DatabaseEntryName, manifest.DatabaseSha256, contents, findings);
        if (manifest.PreferencesSha256 is { } preferences)
        {
            Verify(BackupContentPolicy.PreferencesEntryName, preferences, contents, findings);
        }

        foreach (var artwork in manifest.PersonalArtwork)
        {
            Verify(artwork.RelativePath, artwork.Sha256, contents, findings);
        }
    }

    private static void Verify(
        string entryName,
        string expectedSha256,
        Dictionary<string, byte[]> contents,
        List<RestoreFinding> findings)
    {
        if (!contents.TryGetValue(entryName, out var bytes))
        {
            findings.Add(new RestoreFinding(RestoreFindingKind.MissingEntry, entryName));
            return;
        }

        if (!Convert.ToHexString(SHA256.HashData(bytes))
            .Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new RestoreFinding(RestoreFindingKind.HashMismatch, entryName));
        }
    }
}
