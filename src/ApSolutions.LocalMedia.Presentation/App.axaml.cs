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
            var window = new Window
            {
                Width = 1180,
                Height = 760,
                MinWidth = 900,
                MinHeight = 600,
                Title = GetResourceText(this, "ProductDisplayName"),
                Content = shell,
            };
            ApplyDesignedChrome(window);
            BackdropService?.TryApply(window);
            WindowConfigurator?.Invoke(window);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// The height of the application's own title bar, in the one place both halves of it can read.
    /// </summary>
    /// <remarks>
    /// The window asks the platform to extend its client area by this much, and <c>ShellView</c>
    /// draws a row of the same height into it. Written twice they would disagree the first time the
    /// design moved one of them, so a test asserts the shell's first row against this number.
    /// </remarks>
    public const double TitleBarHeight = 44;

    /// <summary>
    /// Gives a window the title bar the design draws instead of the system's own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype puts a 44 px bar above everything, so the window is one unbroken surface from
    /// the top down rather than a system caption sitting on a different colour. Windows keeps drawing
    /// minimise, maximise, close and the window's title over the extended area, which is why this
    /// adds no control anybody has to press and the autonomous walk's inventory is untouched, and why
    /// <c>ShellView</c> does not repeat the product name inside it.
    /// </para>
    /// <para>
    /// <c>ExtendClientAreaChromeHints</c> does not exist in Avalonia 12.1.1 — these two properties
    /// are the whole surface it kept — so what the chrome does was measured with the application open
    /// rather than asked for.
    /// </para>
    /// <para>
    /// Separate from the window's construction so it can be asserted: everything else here runs only
    /// under a desktop lifetime, which no headless suite has.
    /// </para>
    /// </remarks>
    public static void ApplyDesignedChrome(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.ExtendClientAreaToDecorationsHint = true;
        window.ExtendClientAreaTitleBarHeightHint = TitleBarHeight;
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
