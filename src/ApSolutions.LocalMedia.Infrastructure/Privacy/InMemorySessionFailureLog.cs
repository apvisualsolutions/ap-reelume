// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Privacy;

namespace ApSolutions.LocalMedia.Infrastructure.Privacy;

/// <summary>
/// The session's failures, in memory, bounded, and never written anywhere.
/// </summary>
/// <remarks>
/// ARQ-004. Failures arrive from whichever thread was running the work — a command's continuation, a
/// task nobody awaited, the handler of last resort — so every entry point here is taken under the
/// lock. The ceiling is the other half of it: an application failing a new way every second must not
/// turn this into a list that stops growing only when the process does.
/// </remarks>
public sealed class InMemorySessionFailureLog : ISessionFailureLog
{
    /// <summary>
    /// How many distinct codes are kept. Past this the log stops taking new ones and keeps counting
    /// the ones it has, which is the behaviour that loses the least: a code already seen is one whose
    /// count still means something, and a flood of new codes is itself visible as the ceiling.
    /// </summary>
    public const int Ceiling = 32;

    private readonly Lock _sync = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Record(string code, Exception? exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        lock (_sync)
        {
            if (_entries.TryGetValue(code, out var existing))
            {
                existing.Occurrences++;
                return;
            }

            if (_entries.Count >= Ceiling)
            {
                return;
            }

            // The first exception under a code is the one kept. A later one under the same code is
            // the same failure happening again, and its message is no more allowed to travel than
            // the first one's was.
            _entries[code] = new Entry(exception);
        }
    }

    public IReadOnlyList<DiagnosticsErrorSample> Samples()
    {
        lock (_sync)
        {
            return
            [
                .. _entries
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new DiagnosticsErrorSample(
                        pair.Key,
                        pair.Value.Exception,
                        pair.Value.Occurrences)),
            ];
        }
    }

    private sealed class Entry(Exception? exception)
    {
        public Exception? Exception { get; } = exception;

        public int Occurrences { get; set; } = 1;
    }
}
