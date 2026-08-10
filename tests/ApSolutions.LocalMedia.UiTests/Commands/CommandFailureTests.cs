// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Catalog;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Commands;

/// <summary>
/// What happens to the failure when the work behind a button does not succeed.
/// </summary>
/// <remarks>
/// ARQ-004. Every command surface in this application was its own private class implementing
/// <c>ICommand</c> with <c>async void Execute</c> — twenty-four of them, and not one caught anything.
/// An <c>async void</c> that throws does not return the failure to its caller: it rethrows it on
/// whatever synchronization context was current, which on the interface thread is the application
/// itself. So the choice was never between handling it and not handling it. It was between handling
/// it and ending the process.
/// </remarks>
public sealed class CommandFailureTests
{
    /// <summary>
    /// The measurement that justifies the whole task, taken through a real view model rather than a
    /// contrived one: the failure leaves the command entirely and lands on the context, where the
    /// only thing waiting for it is the process.
    /// </summary>
    /// <remarks>
    /// This describes what the application does today, not what it should do — it is green because
    /// the defect is real. The second half of ARQ-004 replaces the twenty-four private command
    /// classes with one that catches, and when it lands this assertion inverts: nothing escapes to
    /// the context, and the failure is found where the command put it instead.
    /// </remarks>
    [Fact]
    public void A_command_whose_work_fails_throws_it_at_the_application_instead_of_the_surface()
    {
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var viewModel = new WatchStatusViewModel(
                _ => throw new InvalidOperationException("the catalogue refused the change"));

            viewModel.MarkWatchedCommand.Execute(null);

            var escaped = Assert.Single(context.Escaped);
            Assert.IsType<InvalidOperationException>(escaped);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>
    /// A context that catches what an <c>async void</c> hands it, so the measurement can be taken
    /// without the failure doing to the test runner what it would do to the application.
    /// </summary>
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly List<Exception> _escaped = [];

        public IReadOnlyList<Exception> Escaped => _escaped;

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);
            try
            {
                d(state);
            }
            catch (Exception exception)
            {
                _escaped.Add(exception);
            }
        }

        public override void Send(SendOrPostCallback d, object? state) => Post(d, state);
    }
}
