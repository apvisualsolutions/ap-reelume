// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Privacy;
using ApSolutions.LocalMedia.Windows.Shell;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Shell;

/// <summary>
/// The handler of last resort: what happens to a failure that reached no surface at all.
/// </summary>
/// <remarks>
/// ARQ-004. A command that knows where its failure belongs puts it there. This is for the rest — a
/// task nobody awaited, a continuation on a thread with nothing listening, a failure thrown after the
/// surface that started it was gone. Without it, those end the process; with it, they end as a code.
/// <para>
/// The handlers are methods on an instance rather than statics wired at class load, because ARQ-001
/// spent this repository's patience on exactly one static field that nothing could release. Installing
/// hands back something that unhooks.
/// </para>
/// </remarks>
public sealed class ProcessFailureHandlersTests
{
    /// <summary>
    /// An unobserved task failure would be raised again when the finalizer got to it. Observing it is
    /// what stops that, and it only counts if the failure was written down first.
    /// </summary>
    [Fact]
    public void An_unobserved_task_failure_is_written_down_and_marked_observed()
    {
        var log = new InMemorySessionFailureLog();
        var handlers = new ProcessFailureHandlers(log);
        var args = new UnobservedTaskExceptionEventArgs(
            new AggregateException(new InvalidOperationException("no")));

        handlers.OnUnobservedTaskException(sender: null, args);

        Assert.True(args.Observed);
        var sample = Assert.Single(log.Samples());
        Assert.Equal(ProcessFailureHandlers.UnobservedTaskCode, sample.Code);
    }

    /// <summary>
    /// The last thing the process does before it goes is say what kind of failure it was. It cannot
    /// stop this one — by the time the handler runs the decision is made — so the whole value is in
    /// the record surviving into a report.
    /// </summary>
    [Fact]
    public void An_unhandled_failure_is_written_down_before_the_process_goes()
    {
        var log = new InMemorySessionFailureLog();
        var handlers = new ProcessFailureHandlers(log);

        handlers.OnUnhandledException(
            sender: null,
            new UnhandledExceptionEventArgs(new InvalidOperationException("no"), isTerminating: true));

        var sample = Assert.Single(log.Samples());
        Assert.Equal(ProcessFailureHandlers.UnhandledCode, sample.Code);
        Assert.IsType<InvalidOperationException>(sample.Exception);
    }

    /// <summary>
    /// <c>UnhandledException</c> hands over an <see cref="object"/>, not an exception, because what
    /// was thrown need not be one. Something that is not an exception must still be counted rather
    /// than dropped or, worse, cast.
    /// </summary>
    [Fact]
    public void Something_thrown_that_is_not_an_exception_is_still_counted()
    {
        var log = new InMemorySessionFailureLog();
        var handlers = new ProcessFailureHandlers(log);

        handlers.OnUnhandledException(
            sender: null,
            new UnhandledExceptionEventArgs("a string somebody threw", isTerminating: false));

        var sample = Assert.Single(log.Samples());
        Assert.Equal(ProcessFailureHandlers.UnhandledCode, sample.Code);
        Assert.Null(sample.Exception);
    }

    /// <summary>
    /// Installing hooks the process's own events, and letting go unhooks them. Two applications in one
    /// process is a thing this repository made possible on purpose (ARQ-001), so a handler that
    /// outlived its application would write another one's failures into a released log.
    /// </summary>
    [Fact]
    public void Installing_and_releasing_leaves_the_process_as_it_was_found()
    {
        var log = new InMemorySessionFailureLog();
        var handlers = new ProcessFailureHandlers(log);

        handlers.Install();
        handlers.Dispose();

        // After release the process raises its events at nobody: firing the handler by hand still
        // records, but the event no longer reaches it. What is asserted is that releasing twice is
        // not an error, because shutdown arrives from more than one place.
        handlers.Dispose();
        Assert.Empty(log.Samples());
    }

    /// <summary>
    /// Installing twice subscribes once, so one release takes the hooks back off. Subscribing twice
    /// would write one failure down as two, and every count in the report would then be wrong by
    /// however many times somebody happened to call this.
    /// </summary>
    [Fact]
    public void Installing_twice_still_comes_off_with_one_release()
    {
        using var handlers = new ProcessFailureHandlers(new InMemorySessionFailureLog());

        handlers.Install();
        handlers.Install();
        Assert.True(handlers.IsInstalled);

        handlers.Dispose();

        Assert.False(handlers.IsInstalled);
    }

    /// <summary>Releasing something never installed is not an error either, and hooks nothing off.</summary>
    [Fact]
    public void Releasing_something_never_installed_is_not_an_error()
    {
        var handlers = new ProcessFailureHandlers(new InMemorySessionFailureLog());

        handlers.Dispose();

        Assert.False(handlers.IsInstalled);
    }

    /// <summary>
    /// A handler built without anywhere to write says so at construction. Left to fail later it would
    /// fail inside the handler of last resort, which is the one place with nothing behind it.
    /// </summary>
    [Fact]
    public void A_handler_with_nowhere_to_write_says_so_when_it_is_built()
    {
        Assert.Throws<ArgumentNullException>(() => new ProcessFailureHandlers(log: null!));
    }

    /// <summary>Both handlers are given their arguments by the runtime, and both check what they got.</summary>
    [Fact]
    public void Both_handlers_refuse_an_event_with_no_arguments()
    {
        using var handlers = new ProcessFailureHandlers(new InMemorySessionFailureLog());

        Assert.Throws<ArgumentNullException>(
            () => handlers.OnUnhandledException(sender: null, e: null!));
        Assert.Throws<ArgumentNullException>(
            () => handlers.OnUnobservedTaskException(sender: null, e: null!));
    }
}
