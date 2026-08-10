// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Commands;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Commands;

/// <summary>
/// The other half of the same defect: work an event started rather than a button.
/// </summary>
/// <remarks>
/// ARQ-004. A handler whose signature returns <see langword="void"/> has no task for anybody to await
/// either, so a failure inside it goes exactly where a command's used to go — the interface thread,
/// with nothing waiting for it. There were three of these: a catalogue click, a route change, and a
/// folder leaving the library.
/// </remarks>
public sealed class GuardedEventTests
{
    [Fact]
    public void Work_that_fails_does_not_reach_the_thread_that_started_it()
    {
        var context = new RecordingContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            GuardedEvent.Run(() => throw new InvalidOperationException("no"));

            Assert.Empty(context.Escaped);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Fact]
    public void Whoever_asked_to_be_told_about_a_failure_is_told()
    {
        Exception? told = null;

        GuardedEvent.Run(() => throw new InvalidOperationException("no"), exception => told = exception);

        Assert.IsType<InvalidOperationException>(told);
    }

    [Fact]
    public void Work_that_succeeds_runs_and_tells_nobody_anything()
    {
        var ran = false;
        Exception? told = null;

        GuardedEvent.Run(
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            exception => told = exception);

        Assert.True(ran);
        Assert.Null(told);
    }

    [Fact]
    public void A_handler_with_no_work_says_so()
    {
        Assert.Throws<ArgumentNullException>(() => GuardedEvent.Run(work: null!));
    }

    private sealed class RecordingContext : SynchronizationContext
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
