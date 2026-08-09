namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

public sealed class TmdbRateLimiter : IDisposable
{
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly SemaphoreSlim _providerGate = new(1, 1);

    public TmdbRateLimiter(Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _delay = delay ?? ((duration, token) => Task.Delay(duration, token));
    }

    public async Task<T> RunAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _providerGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _providerGate.Release();
        }
    }

    public Task WaitForRetryAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        _delay(delay, cancellationToken);

    public void Dispose() => _providerGate.Dispose();
}
