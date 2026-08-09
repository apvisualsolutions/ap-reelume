using System.ComponentModel;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

/// <summary>
/// The identity a scan captures: the stable NTFS id joined with the bounded content fingerprint.
/// Either half may fail on its own — a volume without stable ids, a file another process holds —
/// without costing the other half; a file that yields neither is simply a file without identity,
/// and reconciliation treats it as such.
/// </summary>
public sealed class CompositeFileIdentityProvider : IFileIdentityProvider
{
    private readonly IFileIdentityProvider _stable;
    private readonly IFileIdentityProvider _fingerprint;

    public CompositeFileIdentityProvider(
        IFileIdentityProvider stable,
        IFileIdentityProvider fingerprint)
    {
        _stable = stable ?? throw new ArgumentNullException(nameof(stable));
        _fingerprint = fingerprint ?? throw new ArgumentNullException(nameof(fingerprint));
    }

    public async Task<FileIdentity> GetAsync(
        string path,
        TechnicalMetadata technicalMetadata,
        CancellationToken cancellationToken = default)
    {
        string? volumeId = null;
        string? fileId = null;
        string? fingerprint = null;
        try
        {
            var stable = await _stable.GetAsync(path, technicalMetadata, cancellationToken).ConfigureAwait(false);
            volumeId = stable.VolumeId;
            fileId = stable.FileId;
        }
        catch (Exception exception) when (IsIdentityFailure(exception))
        {
        }

        try
        {
            var sampled = await _fingerprint.GetAsync(path, technicalMetadata, cancellationToken).ConfigureAwait(false);
            fingerprint = sampled.Fingerprint;
        }
        catch (Exception exception) when (IsIdentityFailure(exception))
        {
        }

        return new FileIdentity(volumeId, fileId, fingerprint);
    }

    private static bool IsIdentityFailure(Exception exception) => exception
        is IOException
        or UnauthorizedAccessException
        or Win32Exception
        or PlatformNotSupportedException;
}
