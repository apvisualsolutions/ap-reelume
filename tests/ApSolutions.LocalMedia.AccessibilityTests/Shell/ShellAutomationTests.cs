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

    /// <summary>
    /// Six since 2026-08-30, when Courses joined the rail (CRS-003). Every one of them is walked
    /// here rather than counted: the assertion inside the loop is that pressing Enter on the
    /// button actually moves the route, so a seventh entry that looked right and navigated
    /// nowhere would still fail.
    /// </summary>
    [AvaloniaFact]
    public void Six_destinations_have_names_roles_states_and_complete_keyboard_navigation()
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
        Assert.Equal(6, navigationButtons.Length);

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

    /// <summary>
    /// The surface that names the application announces itself and carries the signature.
    /// </summary>
    /// <remarks>
    /// Mounted from <c>CreditsView</c> rather than looked for inside the shell, and the move is the
    /// reason: the brand sat at the foot of a 248 px navigation rail until 2026-08-22, when the rail
    /// became 64 px of pictograms with room for neither the name nor the signature. It lives on
    /// Credits now — the screen that is about the application, and the one carrying TMDB's logo,
    /// whose terms want it less prominent than the product's own name. Inside the shell that screen
    /// is behind an invisible settings page, so its descendants are not in the visual tree at all,
    /// and a search there would have failed for the wrong reason.
    /// </remarks>
    [AvaloniaFact]
    public void About_brand_surface_has_an_accessible_name_and_contains_the_publisher_signature()
    {
        var assembly = Assembly.Load(PresentationAssemblyName);
        ApplySpanishResources(assembly);
        var credits = (Control)Activator.CreateInstance(
            RequireType(assembly, "ApSolutions.LocalMedia.Presentation.About.CreditsView"))!;
        var window = new Window
        {
            Width = 1024,
            Height = 720,
            Content = credits,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var about = credits.GetVisualDescendants()
            .OfType<Control>()
            .Single(control => control.Name == "AboutBrandSurface");

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(about)));
        Assert.Contains(
            about.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "by AP Solutions");
        Assert.Contains(
            about.GetVisualDescendants().OfType<TextBlock>(),
            text => text.Text == "AP Reelume");
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
