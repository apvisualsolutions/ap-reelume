// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Windows.Shell;
using ApSolutions.LocalMedia.Windows.Windowing;
using Avalonia;

namespace ApSolutions.LocalMedia.Windows;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.ShellFactory = CompositionRoot.CreateShell;
        App.BackdropService = new MicaBackdropService();
        App.WindowConfigurator = CompositionRoot.ConfigureWindow;

        // "Open with…" hands the path as the first positional argument. Nothing is imported: the
        // activation only asks the coordinator to play what was handed over.
        CompositionRoot.PendingActivationPath = FileActivationHandler.Parse(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
