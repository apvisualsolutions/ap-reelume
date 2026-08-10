// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Privacy;

/// <summary>
/// What went wrong in this session, held as codes so a diagnostics report has something true to say.
/// </summary>
/// <remarks>
/// ARQ-004. A command surface that fails has two possible destinations: its own error state, or
/// nowhere. Only two of this application's twenty-four surfaces own any error state, so catching the
/// failure without somewhere to put it would just be a quieter way of losing it.
/// <para>
/// It lives in memory and for one run. A failure record is exactly the kind of file that grows
/// quietly on somebody's machine and outlives the reason it was written, and this application does
/// not have a story for deleting one. The report that reads this is built only after consent, and it
/// keeps type names rather than messages — a message is written by whoever threw it, so there is no
/// knowing in advance what it decided to include.
/// </para>
/// </remarks>
public interface ISessionFailureLog
{
    /// <summary>
    /// Notes one failure under a code the application chose. The same code twice is one entry with a
    /// count, because work that fails on a timer would otherwise be the only thing the log holds.
    /// </summary>
    void Record(string code, Exception? exception);

    /// <summary>What has been noted so far, ready to be handed to the report builder.</summary>
    IReadOnlyList<DiagnosticsErrorSample> Samples();
}
