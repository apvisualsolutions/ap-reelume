// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Infrastructure.Privacy;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Privacy;

/// <summary>
/// Where a failure goes when no surface knows how to show it.
/// </summary>
/// <remarks>
/// ARQ-004. Twenty-four command surfaces ran their work through <c>async void</c> and caught nothing,
/// and only two of them own any error state at all. So catching the failure is not enough on its own:
/// something has to hold what the surface cannot say, or the fix is only a quieter way to lose it.
/// <para>
/// This holds it in memory, for this session, as a code and a count. Nothing is written to disk: a
/// failure record is the sort of file that grows quietly on somebody's machine, and the report that
/// reads this already asks for consent before it is built at all. What travels is what the allowlist
/// permits — the chain of type names — and never the message, which is written by whoever threw it.
/// </para>
/// </remarks>
public sealed class SessionFailureLogTests
{
    [Fact]
    public void A_recorded_failure_becomes_a_sample_the_report_can_be_built_from()
    {
        var log = new InMemorySessionFailureLog();

        log.Record("command:markWatched", new InvalidOperationException("no"));

        var sample = Assert.Single(log.Samples());
        Assert.Equal("command:markWatched", sample.Code);
        Assert.Equal(1, sample.Occurrences);
    }

    /// <summary>
    /// The same failure twice is one entry with a count, not two entries. A loop that fails on every
    /// tick would otherwise fill the log with one thing and push everything else out of it.
    /// </summary>
    [Fact]
    public void The_same_failure_twice_is_counted_rather_than_repeated()
    {
        var log = new InMemorySessionFailureLog();

        log.Record("command:markWatched", new InvalidOperationException("no"));
        log.Record("command:markWatched", new InvalidOperationException("no again"));

        var sample = Assert.Single(log.Samples());
        Assert.Equal(2, sample.Occurrences);
    }

    /// <summary>
    /// The log is bounded. An application that fails in a new way every second must not turn this into
    /// a list that only stops growing when the process does.
    /// </summary>
    [Fact]
    public void The_log_stops_growing_at_its_ceiling()
    {
        var log = new InMemorySessionFailureLog();

        for (var i = 0; i < InMemorySessionFailureLog.Ceiling * 3; i++)
        {
            log.Record($"command:{i}", new InvalidOperationException("no"));
        }

        Assert.Equal(InMemorySessionFailureLog.Ceiling, log.Samples().Count);
    }

    /// <summary>
    /// The claim that matters: an exception message is written by whoever threw it, so it can carry a
    /// path, a title, or a name from somebody's library. The report is built from the type chain, and
    /// the message never reaches it.
    /// </summary>
    [Fact]
    public void The_report_built_from_a_recorded_failure_carries_types_and_never_the_message()
    {
        var log = new InMemorySessionFailureLog();
        log.Record(
            "command:rename",
            new InvalidOperationException(@"D:\Films\The Thing (1982)\The Thing.mkv is in use"));

        var report = new AllowlistedDiagnosticsBuilder().Build(
            new DiagnosticsConsent(IsGranted: true, DateTimeOffset.UnixEpoch),
            Inputs(log.Samples()));

        Assert.NotNull(report);
        var entry = Assert.Single(report!.Errors);
        Assert.Contains("InvalidOperationException", entry.Type, StringComparison.Ordinal);
        var written = DiagnosticsSerialization.Serialize(report);
        Assert.DoesNotContain("The Thing", written, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".mkv", written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Failures arrive from whichever thread was running the work, so the log has to take that.</summary>
    [Fact]
    public void Failures_recorded_from_many_threads_are_all_counted()
    {
        var log = new InMemorySessionFailureLog();

        Parallel.For(0, 200, _ => log.Record("command:scan", new InvalidOperationException("no")));

        var sample = Assert.Single(log.Samples());
        Assert.Equal(200, sample.Occurrences);
    }

    private static DiagnosticsInputs Inputs(IReadOnlyList<DiagnosticsErrorSample> errors) => new(
        AppVersion: "0.1.0",
        WindowsVersion: "10.0.26200",
        RuntimeVersion: "10.0.0",
        Locale: "es-ES",
        HardwareAccelerationAvailable: true,
        HdrDisplayPresent: false,
        AudioEndpointCount: 2,
        LibraryItemCount: 10,
        RootCount: 1,
        Errors: errors,
        History: [],
        SearchTerms: []);
}
