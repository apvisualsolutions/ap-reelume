using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface IFileIdentityProvider
{
    Task<FileIdentity> GetAsync(
        string path,
        TechnicalMetadata technicalMetadata,
        CancellationToken cancellationToken = default);
}
