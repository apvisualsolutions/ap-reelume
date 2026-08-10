// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Commands;

/// <summary>
/// Work an event started, kept from becoming the end of the process.
/// </summary>
/// <remarks>
/// ARQ-004. <see cref="AsyncRelayCommand"/> covers what a button starts. This covers the rest of the
/// same defect: a handler whose signature returns <see langword="void"/> has no task for anybody to
/// await either, so a failure inside it is rethrown on the interface thread with nothing there to
/// catch it. There were three — a catalogue click, a route change, and a folder leaving the library.
/// <para>
/// The handler stays a plain <see langword="void"/> method and the awaiting happens here, which is
/// the whole point: one <c>async void</c> that catches is worth more than three that do not.
/// </para>
/// </remarks>
public static class GuardedEvent
{
    /// <summary>
    /// Runs the work and keeps whatever went wrong, handing it on when the caller said where.
    /// </summary>
    /// <remarks>
    /// The check is here and the awaiting is next door for a reason worth writing down: inside an
    /// <c>async void</c>, a guard clause is not a guard. It throws into the state machine, which
    /// posts it to the synchronization context like any other failure, so the caller that passed
    /// nothing never hears about it. Splitting the method is what makes the check reach whoever made
    /// the mistake.
    /// </remarks>
    public static void Run(Func<Task> work, Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(work);
        RunCore(work, onFailure);
    }

    private static async void RunCore(Func<Task> work, Action<Exception>? onFailure)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            onFailure?.Invoke(exception);
        }
    }
}
