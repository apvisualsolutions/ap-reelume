using System.Diagnostics;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>
/// Executes each command once, whichever input method produced it. The same command arriving from a
/// second origin inside the coalescing window is dropped, which is what stops a media key that the
/// focused window also delivers as a key press from acting twice.
/// </summary>
public sealed class InputCommandRouter : IInputCommandRouter, IDisposable
{
    private readonly Func<PlaybackInputCommand, CancellationToken, Task> _execute;
    private readonly TimeSpan _coalescingWindow;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<PlaybackInputCommand, long> _lastAccepted = [];
    private readonly List<PlaybackInputCommand> _executed = [];

    public InputCommandRouter(
        Func<PlaybackInputCommand, CancellationToken, Task> execute,
        TimeSpan? coalescingWindow = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _coalescingWindow = coalescingWindow ?? TimeSpan.FromMilliseconds(250);
    }

    public IReadOnlyList<PlaybackInputCommand> Executed
    {
        get
        {
            lock (_executed)
            {
                return [.. _executed];
            }
        }
    }

    /// <summary>Origins that produced a command that was dropped as a duplicate.</summary>
    public IReadOnlyList<InputOrigin> Suppressed { get; private set; } = [];

    public async Task<bool> DispatchAsync(
        PlaybackInputCommand command,
        InputOrigin origin,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command))
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = Stopwatch.GetTimestamp();
            if (_lastAccepted.TryGetValue(command, out var previous)
                && Stopwatch.GetElapsedTime(previous, now) < _coalescingWindow)
            {
                Suppressed = [.. Suppressed, origin];
                return false;
            }

            _lastAccepted[command] = now;
            await _execute(command, cancellationToken).ConfigureAwait(false);
            lock (_executed)
            {
                _executed.Add(command);
            }

            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
