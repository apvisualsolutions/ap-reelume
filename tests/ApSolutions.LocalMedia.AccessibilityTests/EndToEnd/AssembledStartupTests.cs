// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Startup;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Shell;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// ARQ-005: what the window has to show while the database is being made ready.
/// </summary>
/// <remarks>
/// The interface thread used to migrate the database before anything could be handed to the window,
/// so there was nothing on screen until it finished — and the work does not yield, which
/// <c>MigrationYieldTests</c> measures rather than assumes. The correction is not a faster
/// migration: it is a window that exists while the migration happens.
/// </remarks>
[Collection(AssembledShellSuites.Name)]
public sealed class AssembledStartupTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"startup-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    /// <summary>
    /// The property the whole change exists for: something reaches the window before the database
    /// is ready, so the first frame has content rather than waiting for the work to end.
    /// </summary>
    /// <remarks>
    /// The assertion is on a state that is by definition temporary, and the wait afterwards is not
    /// decoration: a test that observes a transient and then leaves is still holding the file the
    /// background work has open, and the teardown deletes that folder. Locally the work finished
    /// first and it passed; on a slower machine it did not, and the failure was an
    /// <c>IOException</c> in <c>Dispose</c> rather than anything about the startup. Observing a
    /// transient means waiting for it to end before leaving.
    /// </remarks>
    [AvaloniaFact]
    public void The_window_is_given_something_to_show_before_the_database_is_ready()
    {
        using var scope = CreateApplication();

        var container = Assert.IsType<ContentControl>(scope.Application.CreateShell());

        Assert.IsType<StartupView>(container.Content);
        _ = AssembledStartup.FinalContent(container);
    }

    /// <summary>
    /// And it is a stand-in, not a destination: the shell takes its place once the work is done.
    /// </summary>
    [AvaloniaFact]
    public void The_shell_takes_the_startup_view_s_place_once_the_database_is_ready()
    {
        using var scope = CreateApplication();

        var settled = AssembledStartup.FinalContent(scope.Application.CreateShell());

        Assert.IsType<ShellView>(settled);
    }

    /// <summary>
    /// The startup view says what it is to whoever cannot see it.
    /// </summary>
    /// <remarks>
    /// The view is built and shown directly rather than caught mid-startup, and the reason is worth
    /// writing down: the assembled version of this check raced and lost — by the time a window is
    /// shown and the dispatcher has run, the preparation has usually finished and the shell has
    /// already taken the view's place, which is the mechanism working. So the name is asked of the
    /// view itself, in a window, because it arrives through a dynamic resource and a control
    /// attached to nothing has nowhere to look one up.
    /// </remarks>
    [AvaloniaFact]
    public void The_startup_view_names_itself_for_assistive_technology()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var startup = new StartupView();
        var window = new Window { Width = 1180, Height = 760, Content = startup };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(
            string.IsNullOrWhiteSpace(AutomationProperties.GetName(startup)),
            "The startup screen reaches the window with no accessible name.");

        window.Close();
    }

    private ApplicationScope CreateApplication()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        Directory.CreateDirectory(_dataRoot);
        return new ApplicationScope(ApplicationHost.Create(new AppDataPaths(_dataRoot)));
    }

    /// <summary>
    /// The host is only asynchronously disposable, and these walks are synchronous by design.
    /// </summary>
    private sealed record ApplicationScope(ApplicationHost Application) : IDisposable
    {
        public void Dispose() => Application.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
