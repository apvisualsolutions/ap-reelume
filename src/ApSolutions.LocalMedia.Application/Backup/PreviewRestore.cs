using ApSolutions.LocalMedia.Domain.Backup;

namespace ApSolutions.LocalMedia.Application.Backup;

/// <summary>Every reason a restore can be refused. Each one names what it refused.</summary>
public enum RestoreFindingKind
{
    UnreadableArchive,
    UnsupportedFormat,
    MissingEntry,
    ForbiddenEntry,
    PathEscape,
    EntryTooLarge,
    HashMismatch,
    DatabaseUnreadable,
    NotEnoughSpace,
    RootConflict,
}

public sealed record RestoreFinding(RestoreFindingKind Kind, string Detail);

/// <summary>What the archive turned out to be, before anything was unpacked anywhere that matters.</summary>
public sealed record RestoreInspection(
    BackupManifest? Manifest,
    IReadOnlyList<RestoreFinding> Findings,
    IReadOnlyList<string> Entries,
    long UncompressedBytes);

/// <summary>
/// The dry run: what a restore would do, said out loud before anything happens. A preview with a single
/// finding is a preview that cannot be confirmed.
/// </summary>
public sealed record RestorePreview(
    BackupManifest? Manifest,
    IReadOnlyList<RestoreFinding> Findings,
    IReadOnlyList<RootRemapDecision> Roots,
    long RequiredBytes,
    long AvailableBytes,
    int MediaFileCount,
    int PathChangeCount)
{
    public bool CanRestore => Manifest is not null && Findings.Count == 0;
}

public sealed record RestoreResult(
    bool Restored,
    RestorePreview Preview,
    string? PreservedDatabasePath,
    string? Failure);

/// <summary>What a staged database says about itself once it can be opened.</summary>
public sealed record RestoreDatabaseFacts(
    bool IsReadable,
    IReadOnlyList<string> Roots,
    IReadOnlyList<string> MediaPaths,
    string Detail);

/// <summary>Reads an incoming archive without unpacking it anywhere the application would use it.</summary>
public interface IBackupValidator
{
    Task<RestoreInspection> InspectAsync(string archivePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unpacks an archive somewhere disposable, answers questions about it, and — only when told — puts it
/// in place of the live database while keeping the one it replaced.
/// </summary>
public interface IStagedRestoreService
{
    long GetAvailableBytes();

    Task<string> ExtractAsync(string archivePath, CancellationToken cancellationToken = default);

    Task<RestoreDatabaseFacts> InspectDatabaseAsync(
        string stagingDirectory,
        CancellationToken cancellationToken = default);

    Task<int> ApplyRemapAsync(
        string stagingDirectory,
        IReadOnlyList<RootRemapDecision> decisions,
        CancellationToken cancellationToken = default);

    Task<string> SwapAsync(string stagingDirectory, CancellationToken cancellationToken = default);

    void Discard(string stagingDirectory);
}

/// <summary>
/// Builds the dry run. Staging happens here because half the questions — is the database readable, which
/// roots does it hold, how many paths would move — can only be answered by unpacking it. Nothing the
/// application uses is touched: the staged copy lives apart and is thrown away unless a restore asks to
/// keep it.
/// </summary>
public sealed class PreviewRestore(IBackupValidator validator, IStagedRestoreService staging)
{
    private readonly IBackupValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly IStagedRestoreService _staging = staging ?? throw new ArgumentNullException(nameof(staging));

    public async Task<RestorePreview> ExecuteAsync(
        string archivePath,
        IReadOnlyList<RootRemap> remaps,
        CancellationToken cancellationToken = default)
    {
        var (preview, staged) = await BuildAsync(archivePath, remaps, keepStaging: false, cancellationToken)
            .ConfigureAwait(false);
        _ = staged;
        return preview;
    }

    /// <summary>
    /// The same dry run, with the option of keeping the staged copy so a confirmed restore does not have
    /// to unpack and validate the archive a second time.
    /// </summary>
    public async Task<(RestorePreview Preview, string? StagingDirectory)> BuildAsync(
        string archivePath,
        IReadOnlyList<RootRemap> remaps,
        bool keepStaging,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(remaps);

        var inspection = await _validator.InspectAsync(archivePath, cancellationToken).ConfigureAwait(false);
        var available = _staging.GetAvailableBytes();
        if (inspection.Manifest is null || inspection.Findings.Count > 0)
        {
            return (Refused(inspection, inspection.Findings, available), null);
        }

        if (inspection.UncompressedBytes > available)
        {
            return (
                Refused(
                    inspection,
                    [new RestoreFinding(
                        RestoreFindingKind.NotEnoughSpace,
                        $"{inspection.UncompressedBytes} bytes needed, {available} available")],
                    available),
                null);
        }

        var staged = await _staging.ExtractAsync(archivePath, cancellationToken).ConfigureAwait(false);
        try
        {
            var facts = await _staging.InspectDatabaseAsync(staged, cancellationToken).ConfigureAwait(false);
            if (!facts.IsReadable)
            {
                _staging.Discard(staged);
                return (
                    Refused(
                        inspection,
                        [new RestoreFinding(RestoreFindingKind.DatabaseUnreadable, facts.Detail)],
                        available),
                    null);
            }

            var decisions = RootRemapPolicy.Resolve(facts.Roots, remaps, Directory.Exists);
            var changes = facts.MediaPaths.Count(path =>
                !RootRemapPolicy.Rewrite(path, decisions).Equals(path, StringComparison.Ordinal));
            var findings = decisions
                .Where(decision => decision.IsBlocking)
                .Select(decision => new RestoreFinding(RestoreFindingKind.RootConflict, decision.NewPath))
                .Distinct()
                .ToArray();
            var preview = new RestorePreview(
                inspection.Manifest,
                findings,
                decisions,
                inspection.UncompressedBytes,
                available,
                facts.MediaPaths.Count,
                changes);
            if (!keepStaging || !preview.CanRestore)
            {
                _staging.Discard(staged);
                return (preview, null);
            }

            return (preview, staged);
        }
        catch
        {
            _staging.Discard(staged);
            throw;
        }
    }

    private static RestorePreview Refused(
        RestoreInspection inspection,
        IReadOnlyList<RestoreFinding> findings,
        long availableBytes) =>
        new(
            inspection.Manifest,
            findings,
            [],
            inspection.UncompressedBytes,
            availableBytes,
            MediaFileCount: 0,
            PathChangeCount: 0);
}
