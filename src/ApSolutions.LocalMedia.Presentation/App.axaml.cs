// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;

namespace ApSolutions.LocalMedia.Presentation;

public sealed partial class App : Avalonia.Application
{
    private const string AssetRoot = "avares://ApSolutions.LocalMedia.Presentation/";

    public static Func<Control>? ShellFactory { get; set; }

    public static IBackdropService? BackdropService { get; set; }

    /// <summary>
    /// Lets the host attach the lifecycle behaviour to the main window. The presentation layer owns
    /// the window; only the host knows about trays and startup entries, so it hooks itself here.
    /// </summary>
    public static Action<Window>? WindowConfigurator { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ApplyLanguage(this, CultureInfo.GetCultureInfo("es-ES"));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shell = ShellFactory?.Invoke() ?? CreateDefaultShell();
            // The title bar is the application's own, 44 px, which is what the prototype puts above
            // everything and where the brand now lives. Windows keeps drawing minimise, maximise and
            // close over the extended area, so this adds no control anybody would have to press and
            // the autonomous walk's inventory is untouched; dragging stays the system's too.
            //
            // Avalonia 12.1.1 has no ExtendClientAreaChromeHints any more - the two properties below
            // are the whole surface it kept - so what the chrome does is measured rather than asked
            // for.
            var window = new Window
            {
                Width = 1180,
                Height = 760,
                MinWidth = 900,
                MinHeight = 600,
                Title = GetResourceText(this, "ProductDisplayName"),
                ExtendClientAreaToDecorationsHint = true,
                ExtendClientAreaTitleBarHeightHint = 44,
                Content = shell,
            };
            BackdropService?.TryApply(window);
            WindowConfigurator?.Invoke(window);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyLanguage(Avalonia.Application application, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(culture);

        var language = culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "en"
            : "es";
        var assetRoot = new Uri(AssetRoot);
        var resources = new ResourceDictionary();
        resources.MergedDictionaries.Add(new ResourceInclude(assetRoot)
        {
            Source = new Uri($"{AssetRoot}Resources/Brand.axaml"),
        });
        resources.MergedDictionaries.Add(new ResourceInclude(assetRoot)
        {
            Source = new Uri($"{AssetRoot}Resources/Strings.{language}.axaml"),
        });
        application.Resources = resources;
    }

    private static ShellView CreateDefaultShell()
    {
        var navigation = new NavigationService();
        return new ShellView
        {
            DataContext = new ShellViewModel(navigation),
        };
    }

    private static string GetResourceText(Avalonia.Application application, string key) =>
        application.TryGetResource(key, application.ActualThemeVariant, out var value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;
}
