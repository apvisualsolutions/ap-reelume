namespace ApSolutions.LocalMedia.Application.Backup;

/// <summary>
/// Packs the same payload a rotating copy holds into one archive a person can keep wherever they like.
/// An export is not a copy: it never publishes into the rotation and never triggers retention, so
/// exporting can never be the reason a stored copy was deleted.
/// </summary>
public sealed class ExportLibrary(
    CreateBackup backups,
    IBackupStore store,
    IBackupArchiveWriter archives)
{
    private readonly CreateBackup _backups = backups ?? throw new ArgumentNullException(nameof(backups));
    private readonly IBackupStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IBackupArchiveWriter _archives = archives ?? throw new ArgumentNullException(nameof(archives));

    public async Task<ExportResult> ExecuteAsync(
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var staging = await _store.CreateStagingAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = await _backups.BuildPayloadAsync(
                staging,
                progress,
                trailingSteps: 0,
                cancellationToken).ConfigureAwait(false);
            var entries = await _archives.WriteAsync(
                destinationPath,
                staging,
                progress,
                cancellationToken).ConfigureAwait(false);
            return new ExportResult(destinationPath, payload.Manifest, entries);
        }
        catch
        {
            // A partially written archive is worse than none: it looks restorable and is not.
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            throw;
        }
        finally
        {
            _store.DiscardStaging(staging);
        }
    }
}
