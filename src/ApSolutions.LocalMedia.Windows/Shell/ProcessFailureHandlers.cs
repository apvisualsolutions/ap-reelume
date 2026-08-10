// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Privacy;

namespace ApSolutions.LocalMedia.Windows.Shell;

/// <summary>
/// The process's own last word about a failure that reached no surface.
/// </summary>
/// <remarks>
/// ARQ-004. A command that knows where its failure belongs puts it there. This exists for what is
/// left: a task nobody awaited, a continuation running where no surface is listening, a failure
/// raised after the screen that started the work has gone. Those were ending the process quietly.
/// <para>
/// What is recorded is a code this class owns and the exception itself; the report built from it
/// keeps the chain of type names and drops the message whole, because a message is written by
/// whoever threw it and there is no knowing what it decided to include. Nothing here formats a
/// string of its own — a handler that built its own line would be the one place the allowlist
/// does not reach.
/// </para>
/// <para>
/// It hooks and unhooks rather than wiring statics at class load. ARQ-001 spent this repository's
/// patience on exactly one static field nothing could release, and two applications in one process
/// is now something the tests rely on.
/// </para>
/// </remarks>
public sealed class ProcessFailureHandlers : IDisposable
{
    /// <summary>A failure that reached the top of the process. It is already too late to stop it.</summary>
    public const string UnhandledCode = "process:unhandled";

    /// <summary>A task that failed with nobody awaiting it, caught before the finalizer raises it again.</summary>
    public const string UnobservedTaskCode = "process:unobservedTask";

    private readonly ISessionFailureLog _log;
    private bool _installed;

    public ProcessFailureHandlers(ISessionFailureLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    /// <summary>
    /// Whether the two handlers are currently in front of the process's own events. It is the state
    /// that makes installing and releasing idempotent, and saying it out loud is what lets anything
    /// check the hooks went on and came back off.
    /// </summary>
    public bool IsInstalled => _installed;

    /// <summary>Puts the two handlers in front of the process's own events.</summary>
    public void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Writes down what got to the top of the process. Nothing here can stop it: by the time this
    /// runs the decision has been made, and the whole value is the record outliving the run.
    /// </summary>
    public void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // What was thrown need not be an exception, so it is asked rather than cast.
        _log.Record(UnhandledCode, e.ExceptionObject as Exception);
    }

    /// <summary>
    /// Writes down a task that failed with nobody awaiting it, and observes it so the finalizer does
    /// not raise it a second time — which is the raise that would end the process.
    /// </summary>
    public void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        _log.Record(UnobservedTaskCode, e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// Takes the handlers back off. Idempotent, because shutdown arrives from the window, from the
    /// tray, and from whoever owns the application, and none of them knows about the others.
    /// </summary>
    public void Dispose()
    {
        if (!_installed)
        {
            return;
        }

        _installed = false;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }
}
