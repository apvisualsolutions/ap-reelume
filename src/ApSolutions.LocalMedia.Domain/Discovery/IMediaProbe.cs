using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface IMediaProbe
{
    Task<TechnicalMetadata> ProbeAsync(string path, CancellationToken cancellationToken = default);
}
