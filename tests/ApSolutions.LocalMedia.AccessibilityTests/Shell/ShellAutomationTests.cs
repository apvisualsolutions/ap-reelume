// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.Shell;

public sealed class ShellAutomationTests
{
    private const string PresentationAssemblyName = "ApSolutions.LocalMedia.Presentation";

    [AvaloniaFact]
    public void Five_destinations_have_names_roles_states_and_complete_keyboard_navigation()
    {
        var assembly = Assembly.Load(PresentationAssemblyName);
        ApplySpanishResources(assembly);
        var (shell, viewModel) = CreateShell(assembly);
        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = shell,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var navigationButtons = shell.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("navigation-destination"))
            .ToArray();
        Assert.Equal(5, navigationButtons.Length);

        foreach (var button in navigationButtons)
        {
            Assert.True(button.Focusable);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            Assert.NotNull(button.Command);
            Assert.NotNull(button.CommandParameter);

            button.Focus();
            Assert.True(button.IsKeyboardFocusWithin);
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();

            var currentRoute = viewModel.GetType().GetProperty("CurrentRoute")?.GetValue(viewModel)?.ToString();
            Assert.Equal(button.CommandParameter.ToString(), currentRoute);
        }

        window.Close();
    }

    [AvaloniaFact]
    public void About_brand_surface_has_an_accessible_name_and_contains_the_publisher_signature()
    {
        var assembly = Assembly.Load(PresentationAssemblyName);
        ApplySpanishResources(assembly);
        var (shell, _) = CreateShell(assembly);
        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = shell,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var about = shell.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "AboutBrandSurface");

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(about)));
        Assert.Contains(
            about.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "by AP Solutions");
        window.Close();
    }

    private static void ApplySpanishResources(Assembly assembly)
    {
        var appType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.App");
        var applyLanguage = appType.GetMethod("ApplyLanguage", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(applyLanguage);
        Assert.NotNull(Avalonia.Application.Current);
        applyLanguage.Invoke(null, [Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES")]);
    }

    private static (Control Shell, object ViewModel) CreateShell(Assembly assembly)
    {
        var serviceType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Navigation.NavigationService");
        var viewModelType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Shell.ShellViewModel");
        var viewType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Shell.ShellView");
        var service = Activator.CreateInstance(serviceType);
        var viewModel = Activator.CreateInstance(viewModelType, service);
        var shell = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(viewType));
        Assert.NotNull(viewModel);
        shell.DataContext = viewModel;
        return (shell, viewModel);
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }
}
