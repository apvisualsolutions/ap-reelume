// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Windows.Shell;
using ApSolutions.LocalMedia.Windows.Windowing;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = ApplicationHost.Create(new AppDataPaths());

        // ARQ-004. The net goes up before anything is asked to run. What a command surface can show,
        // it shows; what reaches the top of the process instead ends as a code rather than as the end
        // of the process. It is installed here because this is the only place that owns the process.
        using var failures = new ProcessFailureHandlers(
            host.Services.GetRequiredService<ISessionFailureLog>());
        failures.Install();

        // "Open with…" hands the path as the first positional argument. Nothing is imported: the
        // activation only asks the coordinator to play what was handed over.
        host.PendingActivationPath = FileActivationHandler.Parse(args);

        App.ShellFactory = host.CreateShell;
        App.BackdropService = new MicaBackdropService();
        App.WindowConfigurator = host.ConfigureWindow;

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // ARQ-001. What the application took, it gives back: the media player, the audio
            // adapter, the tray icon, the hardware key registrations and the clients the updater and
            // the metadata provider hold. Windows would reclaim all of it anyway — that is the point.
            // Leaning on the operating system to undo what this process did is not a teardown, it is
            // a bet, and it is the reason a hotkey registration could outlive the window that made it.
            host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
