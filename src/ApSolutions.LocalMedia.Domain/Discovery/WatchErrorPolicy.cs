// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// What an error from a live watcher means for the watching. The distinction is the whole of
/// BUG-012: an overflow is the operating system saying "I dropped changes you will never see",
/// which asks for a full pass over the root and for the watching to go on. Everything else — a root
/// that stopped answering, a handle that closed — really is the end of that watcher.
/// </summary>
public static class WatchErrorPolicy
{
    /// <summary>
    /// True when the error means events were lost rather than that watching cannot continue. Read
    /// as a closed question with one yes: a watcher is only given up on for reasons that are not
    /// this one, because the cost of getting it wrong is a folder that stops being followed in
    /// silence.
    /// </summary>
    public static bool MeansEventsWereLost(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is InternalBufferOverflowException;
    }
}
