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

// The theme variant is one setting on one application, and these three classes all change it. They
// are serialised so that a class reading a theme cannot be reading one another class just replaced —
// a race that would only ever show up on some runs, which is the kind this repository keeps finding
// on CI's second pass.
[Collection("ThemeVariant")]
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

        // And the half that reaches the animations. They cannot ask a service anything — they read a
        // resource — so the service writes the resource, and that write is what reduced motion is.
        // Asserting only the property would leave every animation running at 160 ms with a green
        // test beside it.
        Assert.Equal(
            TimeSpan.Zero,
            Assert.IsType<TimeSpan>(Avalonia.Application.Current.Resources["MotionDuration"]));
    }

    /// <summary>The other half: with motion allowed, the duration is short rather than absent.</summary>
    /// <remarks>
    /// This used to be asserted about <c>MotionDurationStandardMilliseconds</c> in the token file,
    /// by two separate tests, and the application never read that token: the number it uses is
    /// <c>FluentThemeService</c>'s own. So the guarantee was watching a parallel copy, which is the
    /// defect the scalars gate exists to stop. The token was deleted and the guarantee moved here,
    /// onto the property the rest of the application asks.
    /// </remarks>
    [AvaloniaFact]
    public void Motion_that_is_allowed_is_short_rather_than_absent()
    {
        using var directory = new TestDirectory();
        var harness = CreateHarness(directory.SettingsPath, reducedMotion: false);

        Assert.Equal(true, GetProperty(harness.Service, "AnimationsEnabled"));
        var duration = Assert.IsType<TimeSpan>(GetProperty(harness.Service, "MotionDuration"));
        Assert.InRange(duration.TotalMilliseconds, 1, 250);

        // The service holds no number of its own any more: it reads the token the animations read,
        // and writes it back. Compared against the declared value rather than against 160, so the
        // day the design moves it there is one place to change.
        Assert.True(
            Avalonia.Application.Current!.TryGetResource(
                "MotionDuration",
                Avalonia.Application.Current.ActualThemeVariant,
                out var declared));
        Assert.Equal(declared, duration);
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

        foreach (var (name, variant) in HighContrastVariants())
        {
            Avalonia.Application.Current!.RequestedThemeVariant = variant;
            Capture(viewType, viewModelType, harness.Service, 1.0, System.IO.Path.Combine(artifacts, $"appearance-{name}.png"));
        }

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
        // Five theme choices — both high contrasts became pickable on 2026-08-23 — plus the two
        // language choices BUG-011 added; all seven share the option styling.
        Assert.Equal(7, themeButtons.Length);

        var dark = themeButtons.Single(button => button.CommandParameter?.ToString() == "Dark");
        Assert.NotNull(dark.Command);
        dark.Command.Execute(dark.CommandParameter);
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
        window.Close();
    }

    /// <summary>
    /// High contrast is not a fourth preference: nothing in the shell can select it, so a test that
    /// wants to see it has to ask the system's answer for it. That is what the service reports and
    /// what these two variants are.
    /// </summary>
    [AvaloniaFact]
    public void System_high_contrast_overrides_the_preference_and_takes_its_side_from_the_system()
    {
        using var directory = new TestDirectory();
        var (light, dark) = HighContrastVariantPair();

        var lightHarness = CreateHarness(
            directory.SettingsPath,
            reducedMotion: false,
            highContrast: true,
            highContrastIsLight: true);
        Assert.Equal(light, Avalonia.Application.Current!.RequestedThemeVariant);

        // Choosing a theme does not undo a need: the pill is stored, the variant stays put.
        Apply(lightHarness, "Dark");
        Assert.Equal(light, Avalonia.Application.Current.RequestedThemeVariant);

        _ = CreateHarness(
            directory.SettingsPath,
            reducedMotion: false,
            highContrast: true,
            highContrastIsLight: false);
        Assert.Equal(dark, Avalonia.Application.Current.RequestedThemeVariant);

        // And with the system out of high contrast, the stored preference is what applies again.
        _ = CreateHarness(directory.SettingsPath, reducedMotion: false);
        Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current.RequestedThemeVariant);
    }

    private static (ThemeVariant Light, ThemeVariant Dark) HighContrastVariantPair()
    {
        var variants = RequireType(
            Assembly.Load(PresentationAssemblyName),
            "ApSolutions.LocalMedia.Presentation.Theme.AppThemeVariants");
        return (
            Assert.IsType<ThemeVariant>(variants.GetProperty("HighContrastLight")?.GetValue(null)),
            Assert.IsType<ThemeVariant>(variants.GetProperty("HighContrastDark")?.GetValue(null)));
    }

    private static IEnumerable<(string Name, ThemeVariant Variant)> HighContrastVariants()
    {
        var (light, dark) = HighContrastVariantPair();
        yield return ("high-contrast-light", light);
        yield return ("high-contrast-dark", dark);
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

    private static ThemeHarness CreateHarness(string settingsPath, bool reducedMotion) =>
        CreateHarness(settingsPath, reducedMotion, highContrast: false, highContrastIsLight: true);

    private static ThemeHarness CreateHarness(
        string settingsPath,
        bool reducedMotion,
        bool highContrast,
        bool highContrastIsLight)
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
        var highContrastContract = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.IHighContrastService");
        var serviceType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.FluentThemeService");
        var preferenceType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Theme.ThemePreference");
        Assert.Equal(
            ["System", "Light", "Dark", "HighContrastLight", "HighContrastDark"],
            Enum.GetNames(preferenceType));

        var storeType = RequireType(
            infrastructure,
            "ApSolutions.LocalMedia.Infrastructure.Settings.JsonSettingsStore");
        var store = Activator.CreateInstance(storeType, settingsPath);
        Assert.NotNull(store);
        Assert.True(settingsContract.IsInstanceOfType(store));

        var backdrop = CreateProxy(backdropContract, isEnabled: false, isLight: true);
        var motion = CreateProxy(reducedMotionContract, reducedMotion, isLight: true);
        var contrast = CreateProxy(highContrastContract, highContrast, highContrastIsLight);
        var constructor = serviceType.GetConstructor(
            [typeof(Avalonia.Application), settingsContract, backdropContract, reducedMotionContract, highContrastContract]);
        Assert.NotNull(constructor);
        Assert.NotNull(Avalonia.Application.Current);
        var service = constructor.Invoke([Avalonia.Application.Current, store, backdrop, motion, contrast]);
        return new ThemeHarness(service, preferenceType);
    }

    private static object CreateProxy(Type contract, bool isEnabled, bool isLight)
    {
        var proxy = DispatchProxy.Create(contract, typeof(RuntimeInterfaceProxy));
        Assert.NotNull(proxy);
        ((RuntimeInterfaceProxy)proxy).IsEnabled = isEnabled;
        ((RuntimeInterfaceProxy)proxy).IsLight = isLight;
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
        public bool IsEnabled { get; set; }

        public bool IsLight { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.NotNull(targetMethod);

            // Reduced motion and high contrast both answer IsEnabled, so the proxy answers by name
            // rather than by one shared flag: a single flag would have made a high contrast proxy
            // report whatever the motion one was asked for.
            return targetMethod.Name switch
            {
                "get_IsEnabled" => IsEnabled,
                "get_IsLight" => IsLight,
                _ => targetMethod.ReturnType == typeof(bool) ? false : null,
            };
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
