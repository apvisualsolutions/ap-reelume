using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Domain.Backup;
using ApSolutions.LocalMedia.Infrastructure.Backup;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Backup;

/// <summary>
/// What an incoming archive has to prove before anything is unpacked anywhere that matters. Every case
/// here is a refusal, and each one has to name what it refused rather than fail silently.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RestoreValidationTests
{
    [Fact]
    public async Task A_well_formed_archive_passes_with_no_findings()
    {
        using var fixture = await RestoreFixture.CreateAsync();

        var inspection = await new BackupValidator().InspectAsync(
            fixture.ArchivePath,
            TestContext.Current.CancellationToken);

        Assert.Empty(inspection.Findings);
        Assert.NotNull(inspection.Manifest);
        Assert.Equal(BackupManifest.CurrentFormatVersion, inspection.Manifest.FormatVersion);
        Assert.Contains(BackupContentPolicy.DatabaseEntryName, inspection.Entries);
        Assert.True(inspection.UncompressedBytes > 0);
    }

    [Fact]
    public async Task An_archive_from_a_later_format_is_refused_before_anything_else_is_read()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var tampered = await fixture.RewriteManifestAsync(manifest => manifest with { FormatVersion = 99 });

        var inspection = await new BackupValidator().InspectAsync(
            tampered,
            TestContext.Current.CancellationToken);

        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.UnsupportedFormat);
    }

    [Fact]
    public async Task An_archive_with_no_manifest_at_all_is_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var stripped = fixture.Rebuild(entries => entries.Where(entry =>
            entry.Key != BackupManifest.FileName));

        var inspection = await new BackupValidator().InspectAsync(
            stripped,
            TestContext.Current.CancellationToken);

        Assert.Null(inspection.Manifest);
        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.MissingEntry);
    }

    [Fact]
    public async Task An_archive_whose_database_does_not_match_its_manifest_is_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var tampered = fixture.Rebuild(entries => entries.Select(entry =>
            entry.Key == BackupContentPolicy.DatabaseEntryName
                ? new KeyValuePair<string, byte[]>(entry.Key, Encoding.UTF8.GetBytes("not a database"))
                : entry));

        var inspection = await new BackupValidator().InspectAsync(
            tampered,
            TestContext.Current.CancellationToken);

        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.HashMismatch);
    }

    [Fact]
    public async Task An_archive_missing_a_file_its_manifest_promised_is_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var stripped = fixture.Rebuild(entries => entries.Where(entry =>
            !entry.Key.StartsWith(BackupContentPolicy.PersonalArtworkDirectoryName, StringComparison.Ordinal)));

        var inspection = await new BackupValidator().InspectAsync(
            stripped,
            TestContext.Current.CancellationToken);

        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.MissingEntry);
    }

    [Theory]
    [InlineData("../escaped.json")]
    [InlineData("..\\escaped.json")]
    [InlineData("personal-artwork/../../escaped.jpg")]
    [InlineData("C:/Windows/System32/evil.json")]
    [InlineData("/etc/passwd")]
    public async Task An_entry_that_tries_to_escape_the_staging_folder_is_refused(string entryName)
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var attack = fixture.Rebuild(entries => entries.Append(
            new KeyValuePair<string, byte[]>(entryName, Encoding.UTF8.GetBytes("owned"))));

        var inspection = await new BackupValidator().InspectAsync(
            attack,
            TestContext.Current.CancellationToken);

        Assert.Contains(
            inspection.Findings,
            finding => finding.Kind is RestoreFindingKind.PathEscape or RestoreFindingKind.ForbiddenEntry);
        Assert.DoesNotContain(entryName, inspection.Entries);
    }

    [Fact]
    public async Task An_entry_the_policy_does_not_allow_is_refused_by_name()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var attack = fixture.Rebuild(entries => entries.Append(
            new KeyValuePair<string, byte[]>("payload.mp4", Encoding.UTF8.GetBytes("video"))));

        var inspection = await new BackupValidator().InspectAsync(
            attack,
            TestContext.Current.CancellationToken);

        var finding = Assert.Single(
            inspection.Findings,
            item => item.Kind == RestoreFindingKind.ForbiddenEntry);
        Assert.Contains("payload.mp4", finding.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_archive_that_would_unpack_to_far_more_than_it_claims_is_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var bomb = fixture.Rebuild(entries => entries.Select(entry =>
            entry.Key == BackupContentPolicy.PreferencesEntryName
                ? new KeyValuePair<string, byte[]>(entry.Key, new byte[BackupValidator.MaximumEntryBytes + 1])
                : entry));

        var inspection = await new BackupValidator().InspectAsync(
            bomb,
            TestContext.Current.CancellationToken);

        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.EntryTooLarge);
    }

    [Fact]
    public async Task A_file_that_is_not_an_archive_at_all_is_refused_without_throwing()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var notAnArchive = Path.Combine(fixture.WorkingDirectory, "not-a-zip.zip");
        await File.WriteAllTextAsync(
            notAnArchive,
            "this is plain text",
            TestContext.Current.CancellationToken);

        var inspection = await new BackupValidator().InspectAsync(
            notAnArchive,
            TestContext.Current.CancellationToken);

        Assert.Null(inspection.Manifest);
        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.UnreadableArchive);
    }

    [Fact]
    public async Task An_archive_that_is_not_there_is_refused_without_throwing()
    {
        using var fixture = await RestoreFixture.CreateAsync();

        var inspection = await new BackupValidator().InspectAsync(
            Path.Combine(fixture.WorkingDirectory, "absent.zip"),
            TestContext.Current.CancellationToken);

        Assert.Contains(inspection.Findings, finding => finding.Kind == RestoreFindingKind.UnreadableArchive);
    }

    [Fact]
    public async Task A_corrupt_database_inside_a_perfectly_hashed_archive_is_still_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var corrupted = fixture.RebuildWithMatchingHashes(entries => entries.Select(entry =>
            entry.Key == BackupContentPolicy.DatabaseEntryName
                ? new KeyValuePair<string, byte[]>(entry.Key, Encoding.UTF8.GetBytes("SQLite format 3\0 but not really"))
                : entry));

        var preview = await fixture.PreviewAsync(corrupted, []);

        Assert.Contains(preview.Findings, finding => finding.Kind == RestoreFindingKind.DatabaseUnreadable);
        Assert.False(preview.CanRestore);
    }

    [Fact]
    public async Task The_preview_reports_space_and_refuses_when_the_volume_is_too_small()
    {
        using var fixture = await RestoreFixture.CreateAsync();

        var roomy = await fixture.PreviewAsync(fixture.ArchivePath, [], availableBytes: long.MaxValue);
        var cramped = await fixture.PreviewAsync(fixture.ArchivePath, [], availableBytes: 16);

        Assert.True(roomy.RequiredBytes > 0);
        Assert.DoesNotContain(roomy.Findings, finding => finding.Kind == RestoreFindingKind.NotEnoughSpace);
        Assert.Contains(cramped.Findings, finding => finding.Kind == RestoreFindingKind.NotEnoughSpace);
        Assert.False(cramped.CanRestore);
    }

    [Fact]
    public async Task The_preview_simulates_the_remap_without_touching_anything()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var databaseBefore = await File.ReadAllBytesAsync(
            fixture.Paths.DatabasePath,
            TestContext.Current.CancellationToken);

        var preview = await fixture.PreviewAsync(
            fixture.ArchivePath,
            [new RootRemap(fixture.RootPath, "F:\\new-library")]);

        var root = Assert.Single(preview.Roots);
        Assert.Equal(RootRemapStatus.Remapped, root.Status);
        Assert.Equal("F:\\new-library", root.NewPath);
        Assert.Equal(1, preview.MediaFileCount);
        Assert.Equal(1, preview.PathChangeCount);
        Assert.True(preview.CanRestore);
        Assert.Equal(
            databaseBefore,
            await File.ReadAllBytesAsync(fixture.Paths.DatabasePath, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.StagingLeftovers());
    }

    [Fact]
    public async Task Two_roots_aimed_at_one_folder_block_the_restore_in_the_preview()
    {
        using var fixture = await RestoreFixture.CreateAsync(secondRoot: "E:\\archive");

        var preview = await fixture.PreviewAsync(
            fixture.ArchivePath,
            [
                new RootRemap(fixture.RootPath, "F:\\one"),
                new RootRemap("E:\\archive", "F:\\one"),
            ]);

        Assert.Equal(2, preview.Roots.Count);
        Assert.All(preview.Roots, root => Assert.Equal(RootRemapStatus.Conflict, root.Status));
        Assert.Contains(preview.Findings, finding => finding.Kind == RestoreFindingKind.RootConflict);
        Assert.False(preview.CanRestore);
    }

    [Fact]
    public async Task A_root_that_no_longer_exists_is_reported_without_blocking_the_restore()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        fixture.RemoveSourceTree();

        var preview = await fixture.PreviewAsync(fixture.ArchivePath, []);

        var root = Assert.Single(preview.Roots);
        Assert.Equal(RootRemapStatus.Missing, root.Status);
        Assert.True(preview.CanRestore);
    }

    [Fact]
    public async Task A_manifest_that_is_not_json_or_is_json_nothing_is_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var broken = fixture.Rebuild(entries => entries.Select(entry =>
            entry.Key == BackupManifest.FileName
                ? new KeyValuePair<string, byte[]>(entry.Key, Encoding.UTF8.GetBytes("{ not json"))
                : entry));
        var empty = fixture.Rebuild(entries => entries.Select(entry =>
            entry.Key == BackupManifest.FileName
                ? new KeyValuePair<string, byte[]>(entry.Key, Encoding.UTF8.GetBytes("null"))
                : entry));

        var brokenInspection = await new BackupValidator().InspectAsync(
            broken,
            TestContext.Current.CancellationToken);
        var emptyInspection = await new BackupValidator().InspectAsync(
            empty,
            TestContext.Current.CancellationToken);

        Assert.Null(brokenInspection.Manifest);
        Assert.Contains(
            brokenInspection.Findings,
            finding => finding.Kind == RestoreFindingKind.UnsupportedFormat);
        Assert.Null(emptyInspection.Manifest);
        Assert.Contains(
            emptyInspection.Findings,
            finding => finding.Kind == RestoreFindingKind.UnsupportedFormat);
    }

    [Fact]
    public async Task Unpacking_refuses_what_the_validator_would_have_refused()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var staging = fixture.CreateStagedRestoreService();

        var forbidden = fixture.Rebuild(entries => entries.Append(
            new KeyValuePair<string, byte[]>("payload.mp4", Encoding.UTF8.GetBytes("video"))));
        var escaping = fixture.Rebuild(entries => entries.Append(
            new KeyValuePair<string, byte[]>("../escaped.json", Encoding.UTF8.GetBytes("owned"))));

        await Assert.ThrowsAsync<InvalidDataException>(
            () => staging.ExtractAsync(forbidden, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => staging.ExtractAsync(escaping, TestContext.Current.CancellationToken));
        Assert.Empty(fixture.StagingLeftovers());
        Assert.False(File.Exists(Path.Combine(fixture.Paths.DataRoot, "escaped.json")));
    }

    [Fact]
    public async Task A_staged_folder_with_no_database_or_a_broken_one_answers_that_it_is_unreadable()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var staging = fixture.CreateStagedRestoreService();
        var emptyFolder = Path.Combine(fixture.WorkingDirectory, "empty-staging");
        Directory.CreateDirectory(emptyFolder);
        var brokenFolder = Path.Combine(fixture.WorkingDirectory, "broken-staging");
        Directory.CreateDirectory(brokenFolder);
        await File.WriteAllBytesAsync(
            Path.Combine(brokenFolder, BackupContentPolicy.DatabaseEntryName),
            [0x41, 0x50, 0x53, 0x4F, 0x4C],
            TestContext.Current.CancellationToken);

        var missing = await staging.InspectDatabaseAsync(emptyFolder, TestContext.Current.CancellationToken);
        var broken = await staging.InspectDatabaseAsync(brokenFolder, TestContext.Current.CancellationToken);

        Assert.False(missing.IsReadable);
        Assert.False(broken.IsReadable);
        Assert.NotEmpty(broken.Detail);
    }

    [Fact]
    public async Task A_swap_that_cannot_finish_puts_the_original_database_back_and_says_so()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var staging = fixture.CreateStagedRestoreService();
        var before = await File.ReadAllBytesAsync(
            fixture.Paths.DatabasePath,
            TestContext.Current.CancellationToken);
        var staged = await staging.ExtractAsync(fixture.ArchivePath, TestContext.Current.CancellationToken);
        File.Delete(Path.Combine(staged, BackupContentPolicy.DatabaseEntryName));

        await Assert.ThrowsAnyAsync<IOException>(
            () => staging.SwapAsync(staged, TestContext.Current.CancellationToken));

        Assert.True(File.Exists(fixture.Paths.DatabasePath));
        Assert.Equal(
            before,
            await File.ReadAllBytesAsync(fixture.Paths.DatabasePath, TestContext.Current.CancellationToken));
        staging.Discard(staged);
    }

    [Fact]
    public async Task The_staging_folder_refuses_to_discard_anything_that_is_not_its_own()
    {
        using var fixture = await RestoreFixture.CreateAsync();
        var staging = fixture.CreateStagedRestoreService();
        var outside = Path.Combine(fixture.WorkingDirectory, "not-staging");
        Directory.CreateDirectory(outside);

        staging.Discard(string.Empty);
        Assert.Throws<InvalidOperationException>(() => staging.Discard(outside));

        Assert.True(Directory.Exists(outside));
        Assert.True(staging.GetAvailableBytes() > 0);
    }

    [Fact]
    public async Task A_restore_whose_staged_database_cannot_be_inspected_cleans_up_after_itself()
    {
        using var fixture = await RestoreFixture.CreateAsync();

        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.PreviewWithBrokenInspectionAsync());

        Assert.Empty(fixture.StagingLeftovers());
    }

    [Fact]
    public async Task A_root_that_is_still_where_it_was_needs_no_decision()
    {
        using var fixture = await RestoreFixture.CreateAsync();

        var preview = await fixture.PreviewAsync(fixture.ArchivePath, []);

        var root = Assert.Single(preview.Roots);
        Assert.Equal(RootRemapStatus.Unchanged, root.Status);
        Assert.Equal(0, preview.PathChangeCount);
        Assert.True(preview.CanRestore);
    }
}
