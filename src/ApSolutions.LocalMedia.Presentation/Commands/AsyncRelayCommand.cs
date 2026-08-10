// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows.Input;

namespace ApSolutions.LocalMedia.Presentation.Commands;

/// <summary>
/// A button's work, awaited, with the failure kept instead of thrown at the application.
/// </summary>
/// <remarks>
/// ARQ-004. Every command surface here used to declare a private class of its own implementing
/// <see cref="ICommand"/> with <c>async void Execute</c> — twenty-four of them, six constructor
/// shapes, and not one catching anything. An <c>async void</c> that throws does not hand the failure
/// back to its caller: it rethrows it on whatever synchronization context was current, which on the
/// interface thread is the application itself. So the choice was never between handling a failure and
/// leaving it; it was between handling it and ending the process.
/// <para>
/// This catches, always. Where the surface owns error state it is told through
/// <c>onFailure</c> and shows it; where it does not, the failure is still on
/// <see cref="LastFailure"/> and still reaches the session's log through whatever the caller passed.
/// What it never does is let one through, because there is nothing behind it that could survive one.
/// </para>
/// </remarks>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onFailure;

    /// <summary>A command whose work ignores the parameter, which is most of them.</summary>
    public AsyncRelayCommand(
        Func<Task> execute,
        Func<bool>? canExecute = null,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = _ => execute();
        _canExecute = canExecute is null ? null : _ => canExecute();
        _onFailure = onFailure;
    }

    /// <summary>A command whose work is about whatever the surface bound to it.</summary>
    public AsyncRelayCommand(
        Func<object?, Task> execute,
        Func<object?, bool>? canExecute = null,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
        _onFailure = onFailure;
    }

    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// What went wrong the last time this ran, or <see langword="null"/> if the last run was fine.
    /// It exists so a failure is never only in a handler somebody may not have passed.
    /// </summary>
    public Exception? LastFailure { get; private set; }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    /// <summary>
    /// Runs the work, if the answer to <see cref="CanExecute"/> allows it. This is the one
    /// <c>async void</c> the application keeps, and it is the reason the others could go: an
    /// <see cref="ICommand"/> hands back no task for anybody to await, so somewhere the awaiting has
    /// to stop. It stops here, inside a catch.
    /// </summary>
    /// <remarks>
    /// Asking first is not a formality. Nothing about <see cref="ICommand"/> promises that a caller
    /// consulted <see cref="CanExecute"/> — a key binding, a view that does not ask, or code calling
    /// this directly all reach it. One of the classes this replaced relied on the check to keep a
    /// rating outside one to ten from ever reaching the catalogue, and the test that says so is what
    /// caught its absence here.
    /// </remarks>
    public async void Execute(object? parameter)
    {
        LastFailure = null;
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            await _execute(parameter).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            LastFailure = exception;
            _onFailure?.Invoke(exception);
        }
    }

    /// <summary>Says the answer to <see cref="CanExecute"/> may have changed.</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
