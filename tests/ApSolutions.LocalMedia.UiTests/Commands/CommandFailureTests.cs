// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;
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
    /// The measurement that justified the task, now stated as the fix: the failure does not leave the
    /// command. Before this landed, the same assertion held the other way round — one exception on the
    /// context, where the only thing waiting for it was the process.
    /// </summary>
    [Fact]
    public void A_command_whose_work_fails_keeps_the_failure_instead_of_throwing_it_at_the_application()
    {
        var context = new RecordingSynchronizationContext();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var viewModel = new WatchStatusViewModel(
                _ => throw new InvalidOperationException("the catalogue refused the change"));

            viewModel.MarkWatchedCommand.Execute(null);

            Assert.Empty(context.Escaped);
            var command = Assert.IsType<AsyncRelayCommand>(viewModel.MarkWatchedCommand);
            Assert.IsType<InvalidOperationException>(command.LastFailure);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    /// <summary>Where the surface has somewhere to put the failure, it is told rather than left to look.</summary>
    [Fact]
    public void A_surface_that_can_show_a_failure_is_told_about_it()
    {
        Exception? told = null;
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("no"),
            onFailure: exception => told = exception);

        command.Execute(null);

        Assert.IsType<InvalidOperationException>(told);
    }

    /// <summary>
    /// A run that succeeds clears what the run before it left, or a surface would go on showing a
    /// failure that has since been fixed by the very button that fixed it.
    /// </summary>
    [Fact]
    public void A_run_that_succeeds_clears_the_failure_the_run_before_it_left()
    {
        var shouldFail = true;
        var command = new AsyncRelayCommand(() => shouldFail
            ? throw new InvalidOperationException("no")
            : Task.CompletedTask);

        command.Execute(null);
        Assert.NotNull(command.LastFailure);

        shouldFail = false;
        command.Execute(null);

        Assert.Null(command.LastFailure);
    }

    /// <summary>The work can be about whatever the surface bound to it, and so can the answer to CanExecute.</summary>
    [Fact]
    public void A_command_about_its_parameter_is_given_the_parameter()
    {
        object? seen = null;
        var command = new AsyncRelayCommand(
            parameter =>
            {
                seen = parameter;
                return Task.CompletedTask;
            },
            parameter => parameter is not null);

        Assert.False(command.CanExecute(null));
        Assert.True(command.CanExecute("a row"));
        command.Execute("a row");

        Assert.Equal("a row", seen);
    }

    /// <summary>
    /// A command that says it cannot run does not run when it is told to anyway. Nothing about
    /// <c>ICommand</c> promises the caller asked first — a key binding, a view that does not consult,
    /// or code calling it directly all arrive the same way. One of the classes this replaced leaned on
    /// exactly this to keep a rating outside one to ten from reaching the catalogue, and losing the
    /// check was how the migration first went wrong.
    /// </summary>
    [Fact]
    public void A_command_that_cannot_run_does_not_run_when_it_is_told_to_anyway()
    {
        var ran = 0;
        var command = new AsyncRelayCommand(
            () =>
            {
                ran++;
                return Task.CompletedTask;
            },
            () => false);

        command.Execute(null);

        Assert.Equal(0, ran);
    }

    /// <summary>With no answer given, a command can always run: that was every ad-hoc class's default.</summary>
    [Fact]
    public void A_command_with_nothing_to_ask_can_always_run()
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask);

        Assert.True(command.CanExecute(null));
    }

    /// <summary>
    /// The surface is told the answer may have changed. It is the one piece of the ad-hoc classes that
    /// several of them actually used, so it had to survive the move.
    /// </summary>
    [Fact]
    public void Saying_the_answer_may_have_changed_reaches_whoever_asked()
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask);
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }

    /// <summary>A command with no work is a button wired to nothing, and it says so when it is built.</summary>
    [Fact]
    public void A_command_with_no_work_says_so_when_it_is_built()
    {
        Assert.Throws<ArgumentNullException>(
            () => new AsyncRelayCommand(execute: (Func<Task>)null!));
        Assert.Throws<ArgumentNullException>(
            () => new AsyncRelayCommand(execute: (Func<object?, Task>)null!));
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
