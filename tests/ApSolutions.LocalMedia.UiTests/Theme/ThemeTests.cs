// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

public sealed class ThemeTests
{
    private const string PresentationAssemblyName = "ApSolutions.LocalMedia.Presentation";
    private const string InfrastructureAssemblyName = "ApSolutions.LocalMedia.Infrastructure";

    [AvaloniaFact]
    public void System_is_default_and_manual_theme_switches_live_without_restart()
    {
        using var directory = new TestDirectory();
        var harness = CreateHarness(directory.SettingsPath, reducedMotion: false);
        var application = Assert.IsType<HeadlessTestApplication>(Avalonia.Application.Current);

        Assert.Equal("System", GetProperty(harness.Service, "CurrentPreference")?.ToString());
        Assert.Equal(ThemeVariant.Default, application.RequestedThemeVariant);

        var originalApplication = Avalonia.Application.Current;
        Apply(harness, "Light");
        Assert.Same(originalApplication, Avalonia.Application.Current);
        Assert.Equal(ThemeVariant.Light, application.RequestedThemeVariant);

        Apply(harness, "Dark");
        Assert.Same(originalApplication, Avalonia.Application.Current);
        Assert.Equal(ThemeVariant.Dark, application.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Theme_preference_is_written_as_atomic_JSON_and_restored_by_a_new_service()
    {
        using var directory = new TestDirectory();
        var first = CreateHarness(directory.SettingsPath, reducedMotion: false);

        Apply(first, "Dark");

        Assert.True(File.Exists(directory.SettingsPath));
        using (var document = JsonDocument.Parse(File.ReadAllText(directory.SettingsPath)))
        {
            Assert.True(document.RootElement.TryGetProperty("theme.preference", out var persisted));
            Assert.Equal("Dark", persisted.GetString());
        }

        Assert.Empty(Directory.EnumerateFiles(directory.Path, "*.tmp"));

        Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
        var restored = CreateHarness(directory.SettingsPath, reducedMotion: false);
        Assert.Equal("Dark", GetProperty(restored.Service, "CurrentPreference")?.ToString());
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Player_is_always_dark_and_reduced_motion_disables_animation_duration()
    {
        using var directory = new TestDirectory();
        var harness = CreateHarness(directory.SettingsPath, reducedMotion: true);

        Apply(harness, "Light");

        Assert.Equal(ThemeVariant.Dark, GetProperty(harness.Service, "PlayerThemeVariant"));
        Assert.Equal(false, GetProperty(harness.Service, "AnimationsEnabled"));
        Assert.Equal(TimeSpan.Zero, GetProperty(harness.Service, "MotionDuration"));
        Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact]
    public void Appearance_renders_all_theme_contrast_modes_and_scales_at_100_150_200_percent()
    {
        using var directory = new TestDirectory();
        var harness = CreateHarness(directory.SettingsPath, reducedMotion: false);
        var presentation = Assembly.Load(PresentationAssemblyName);
        ApplySpanishResources(presentation);
        var viewType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Settings.AppearanceSettingsView");
        var viewModelType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Settings.AppearanceSettingsViewModel");
        var artifacts = System.IO.Path.Combine(
            RepositoryLayout.Root,
            "artifacts",
            "ui-captures",
            "T3");
        Directory.CreateDirectory(artifacts);

        foreach (var mode in new[] { "System", "Light", "Dark" })
        {
            Apply(harness, mode);
            Capture(viewType, viewModelType, harness.Service, 1.0, System.IO.Path.Combine(artifacts, $"appearance-{mode.ToLowerInvariant()}.png"));
        }

        Avalonia.Application.Current!.RequestedThemeVariant = new ThemeVariant("HighContrast", ThemeVariant.Light);
        Capture(viewType, viewModelType, harness.Service, 1.0, System.IO.Path.Combine(artifacts, "appearance-high-contrast.png"));

        Apply(harness, "Light");
        foreach (var scale in new[] { 1.0, 1.5, 2.0 })
        {
            var percentage = (int)(scale * 100);
            Capture(viewType, viewModelType, harness.Service, scale, System.IO.Path.Combine(artifacts, $"appearance-scale-{percentage}.png"));
        }
    }

    [AvaloniaFact]
    public void Settings_route_hosts_the_appearance_controls_and_their_commands_apply_live()
    {
        using var directory = new TestDirectory();
        var harness = CreateHarness(directory.SettingsPath, reducedMotion: false);
        var presentation = Assembly.Load(PresentationAssemblyName);
        ApplySpanishResources(presentation);
        var navigationContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Navigation.INavigationService");
        var navigationType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Navigation.NavigationService");
        var routeType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Navigation.AppRoute");
        var appearanceViewModelType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Settings.AppearanceSettingsViewModel");
        var shellViewModelType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Shell.ShellViewModel");
        var shellViewType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Shell.ShellView");

        var navigation = Activator.CreateInstance(navigationType);
        var appearance = Activator.CreateInstance(appearanceViewModelType, harness.Service, null);
        var constructor = shellViewModelType.GetConstructor([navigationContract, appearanceViewModelType]);
        Assert.NotNull(constructor);
        var shellViewModel = constructor.Invoke([navigation, appearance]);
        var shell = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(shellViewType));
        shell.DataContext = shellViewModel;

        navigationType.GetMethod("Navigate")!.Invoke(navigation, [Enum.Parse(routeType, "Settings")]);
        Assert.Equal(true, GetProperty(shellViewModel, "IsSettingsVisible"));

        var window = new Window { Width = 1024, Height = 720, Content = shell };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var themeButtons = shell.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("theme-option"))
            .ToArray();
        // Three theme choices plus the two language choices BUG-011 added; all five share the
        // option styling.
        Assert.Equal(5, themeButtons.Length);

        var dark = themeButtons.Single(button => button.CommandParameter?.ToString() == "Dark");
        Assert.NotNull(dark.Command);
        dark.Command.Execute(dark.CommandParameter);
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
        window.Close();
    }

    private static void Capture(Type viewType, Type viewModelType, object themeService, double scale, string path)
    {
        var view = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(viewType));
        view.DataContext = Activator.CreateInstance(viewModelType, themeService, null);
        var window = new Window
        {
            Width = 720,
            Height = 480,
            Content = view,
        };
        window.SetRenderScaling(scale);
        window.Show();
        view.InvalidateMeasure();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        view.InvalidateVisual();
        window.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(path, PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(path));
        window.Close();
    }

    private static ThemeHarness CreateHarness(string settingsPath, bool reducedMotion)
    {
        var presentation = Assembly.Load(PresentationAssemblyName);
        var infrastructure = Assembly.Load(InfrastructureAssemblyName);
        var settingsContract = RequireType(
            Assembly.Load("ApSolutions.LocalMedia.Application"),
            "ApSolutions.LocalMedia.Application.Settings.ISettingsStore");
        var backdropContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IBackdropService");
        var reducedMotionContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IReducedMotionService");
        var serviceType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.FluentThemeService");
        var preferenceType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.ThemePreference");
        Assert.Equal(["System", "Light", "Dark"], Enum.GetNames(preferenceType));

        var storeType = RequireType(
            infrastructure,
            "ApSolutions.LocalMedia.Infrastructure.Settings.JsonSettingsStore");
        var store = Activator.CreateInstance(storeType, settingsPath);
        Assert.NotNull(store);
        Assert.True(settingsContract.IsInstanceOfType(store));

        var backdrop = CreateProxy(backdropContract, reducedMotion: false);
        var motion = CreateProxy(reducedMotionContract, reducedMotion);
        var constructor = serviceType.GetConstructor(
            [typeof(Avalonia.Application), settingsContract, backdropContract, reducedMotionContract]);
        Assert.NotNull(constructor);
        Assert.NotNull(Avalonia.Application.Current);
        var service = constructor.Invoke([Avalonia.Application.Current, store, backdrop, motion]);
        return new ThemeHarness(service, preferenceType);
    }

    private static object CreateProxy(Type contract, bool reducedMotion)
    {
        var proxy = DispatchProxy.Create(contract, typeof(RuntimeInterfaceProxy));
        Assert.NotNull(proxy);
        ((RuntimeInterfaceProxy)proxy).ReducedMotion = reducedMotion;
        return proxy;
    }

    private static void Apply(ThemeHarness harness, string preference)
    {
        var apply = harness.Service.GetType().GetMethod("Apply", [harness.PreferenceType]);
        Assert.NotNull(apply);
        var value = Enum.Parse(harness.PreferenceType, preference);
        apply.Invoke(harness.Service, [value]);
        Assert.Equal(preference, GetProperty(harness.Service, "CurrentPreference")?.ToString());
    }

    private static object? GetProperty(object instance, string propertyName) =>
        instance.GetType().GetProperty(propertyName)?.GetValue(instance);

    private static void ApplySpanishResources(Assembly presentation)
    {
        var appType = RequireType(presentation, "ApSolutions.LocalMedia.Presentation.App");
        var applyLanguage = appType.GetMethod("ApplyLanguage", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(applyLanguage);
        Assert.NotNull(Avalonia.Application.Current);
        applyLanguage.Invoke(null, [Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES")]);
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }

    private sealed record ThemeHarness(object Service, Type PreferenceType);

#pragma warning disable CA1852 // DispatchProxy creates a runtime-derived proxy type.
    private class RuntimeInterfaceProxy : DispatchProxy
    {
        public bool ReducedMotion { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);
            if (targetMethod.Name == "get_IsEnabled")
            {
                return ReducedMotion;
            }

            if (targetMethod.ReturnType == typeof(bool))
            {
                return false;
            }

            return null;
        }
    }
#pragma warning restore CA1852

    private sealed class TestDirectory : IDisposable
    {
        private static readonly string TestRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "APSolutions.LocalMedia.Tests");

        public TestDirectory()
        {
            Path = System.IO.Path.Combine(TestRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string SettingsPath => System.IO.Path.Combine(Path, "settings.json");

        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            var root = System.IO.Path.GetFullPath(TestRoot) + System.IO.Path.DirectorySeparatorChar;
            if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Delete(resolved, recursive: true);
            }
        }
    }
}
