using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

public sealed class ShellLocalizationTests
{
    private const string PresentationAssemblyName = "ApSolutions.LocalMedia.Presentation";

    [Fact]
    public void Startup_is_account_free_and_defaults_to_home()
    {
        var assembly = LoadPresentationAssembly();
        var serviceType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Navigation.NavigationService");
        var viewModelType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Shell.ShellViewModel");

        var service = Activator.CreateInstance(serviceType);
        Assert.NotNull(service);

        var viewModel = Activator.CreateInstance(viewModelType, service);
        Assert.NotNull(viewModel);
        Assert.Equal("Home", viewModelType.GetProperty("CurrentRoute")?.GetValue(viewModel)?.ToString());
        Assert.DoesNotContain(
            assembly.GetReferencedAssemblies(),
            reference => reference.Name?.Contains("Authentication", StringComparison.OrdinalIgnoreCase) is true
                || reference.Name?.Equals("System.Net.Http", StringComparison.OrdinalIgnoreCase) is true);
    }

    [Fact]
    public void Navigation_contract_exposes_exactly_the_five_approved_destinations()
    {
        var assembly = LoadPresentationAssembly();
        var routeType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Navigation.AppRoute");

        Assert.Equal(
            ["Home", "Library", "Review", "Backups", "Settings"],
            Enum.GetNames(routeType));

        var navigationContract = RequireType(
            assembly,
            "ApSolutions.LocalMedia.Presentation.Navigation.INavigationService");
        var navigateMethod = navigationContract.GetMethod("Navigate");
        Assert.NotNull(navigateMethod);
        Assert.Equal(routeType, Assert.Single(navigateMethod.GetParameters()).ParameterType);
    }

    [Fact]
    public void Spanish_and_English_resources_have_identical_keys_and_approved_branding()
    {
        var presentationRoot = GetPresentationRoot();
        var spanishPath = Path.Combine(presentationRoot, "Resources", "Strings.es.axaml");
        var englishPath = Path.Combine(presentationRoot, "Resources", "Strings.en.axaml");
        var brandPath = Path.Combine(presentationRoot, "Resources", "Brand.axaml");

        var spanish = LoadResources(spanishPath);
        var english = LoadResources(englishPath);
        var brand = LoadResources(brandPath);

        Assert.NotEmpty(spanish);
        Assert.Equal(spanish.Keys.Order(), english.Keys.Order());
        Assert.Equal("AP Reelume", brand["ProductDisplayName"]);
        Assert.Equal("by AP Solutions", brand["PublisherSignature"]);
    }

    [Fact]
    public void Visible_shell_text_comes_from_resources_and_signature_is_confined_to_about()
    {
        var presentationRoot = GetPresentationRoot();
        var requiredFiles = new[]
        {
            Path.Combine(presentationRoot, "App.axaml"),
            Path.Combine(presentationRoot, "Shell", "ShellView.axaml"),
        };

        foreach (var path in requiredFiles)
        {
            Assert.True(File.Exists(path), $"Required XAML is missing: {path}");
        }

        var shell = XDocument.Load(requiredFiles[1]);
        var visibleAttributes = shell.Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "Header" or "Title")
            .ToArray();

        Assert.NotEmpty(visibleAttributes);
        Assert.All(
            visibleAttributes,
            attribute => Assert.StartsWith("{", attribute.Value, StringComparison.Ordinal));

        var publisherReferences = shell.Descendants()
            .Where(element => element.Attributes().Any(attribute =>
                attribute.Value.Contains("PublisherSignature", StringComparison.Ordinal)))
            .ToArray();
        Assert.NotEmpty(publisherReferences);
        Assert.All(
            publisherReferences,
            element => Assert.Contains(
                element.AncestorsAndSelf(),
                ancestor => ancestor.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" && attribute.Value == "AboutBrandSurface")));
    }

    [AvaloniaFact]
    public void Shell_renders_in_Spanish_and_English_and_records_exact_captures()
    {
        var assembly = LoadPresentationAssembly();
        var appType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.App");
        var applyLanguage = appType.GetMethod(
            "ApplyLanguage",
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(applyLanguage);
        Assert.NotNull(Avalonia.Application.Current);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            applyLanguage.Invoke(null, [Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName)]);

            var shell = CreateShell(assembly);
            var window = new Window
            {
                Width = 1024,
                Height = 720,
                Content = shell,
            };

            window.Show();
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var artifactPath = Path.Combine(
                GetRepositoryRoot(),
                "artifacts",
                "ui-captures",
                "T2",
                $"shell-{cultureName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
            Assert.True(File.Exists(artifactPath));

            window.Close();
        }
    }

    private static Control CreateShell(Assembly assembly)
    {
        var serviceType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Navigation.NavigationService");
        var viewModelType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Shell.ShellViewModel");
        var viewType = RequireType(assembly, "ApSolutions.LocalMedia.Presentation.Shell.ShellView");
        var service = Activator.CreateInstance(serviceType);
        var viewModel = Activator.CreateInstance(viewModelType, service);
        var view = Activator.CreateInstance(viewType);
        var control = Assert.IsAssignableFrom<Control>(view);
        control.DataContext = viewModel;
        return control;
    }

    private static Assembly LoadPresentationAssembly() => Assembly.Load(PresentationAssemblyName);

    private static Type RequireType(Assembly assembly, string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }

    private static Dictionary<string, string> LoadResources(string path)
    {
        Assert.True(File.Exists(path), $"Resource dictionary is missing: {path}");
        var document = XDocument.Load(path);
        return document.Root!
            .Elements()
            .Where(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"))
            .ToDictionary(
                element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value,
                element => element.Value,
                StringComparer.Ordinal);
    }

    private static string GetPresentationRoot() =>
        Path.Combine(GetRepositoryRoot(), "src", PresentationAssemblyName);

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
