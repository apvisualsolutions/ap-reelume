using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Events;

public sealed record ScanProgressChanged(
    LibraryRootId RootId,
    int EnumeratedCount,
    int ProbeCount,
    string? CurrentPath,
    bool IsCompleted);

public sealed record CatalogChanged(LibraryRootId RootId, int ChangedCount);

public sealed class InProcessApplicationEventPublisher : IApplicationEventPublisher
{
    public event Action<object>? Published;

    public Task PublishAsync<TEvent>(
        TEvent applicationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        Published?.Invoke(applicationEvent);
        return Task.CompletedTask;
    }
}
