using System.Globalization;
using System.Reflection;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Recovery;

public sealed class DatabaseRecoveryViewTests
{
    [AvaloniaFact]
    public void Recovery_view_preserves_paths_and_never_offers_to_overwrite_the_backup()
    {
        var presentation = Assembly.Load("ApSolutions.LocalMedia.Presentation");
        ApplySpanishResources(presentation);
        var viewModelType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Recovery.DatabaseRecoveryViewModel");
        var viewType = RequireType(
            presentation,
            "ApSolutions.LocalMedia.Presentation.Recovery.DatabaseRecoveryView");
        var databasePath = @"C:\Data\APSolutions\LocalMedia\library.db";
        var backupPath = @"C:\Data\APSolutions\LocalMedia\library.db.pre-migration.bak";
        var viewModel = Activator.CreateInstance(viewModelType, databasePath, backupPath, "integrity_check failed");
        Assert.NotNull(viewModel);

        Assert.Equal(false, viewModelType.GetProperty("CanOverwriteBackup")?.GetValue(viewModel));
        var actions = Assert.IsAssignableFrom<System.Collections.IEnumerable>(
            viewModelType.GetProperty("AvailableActions")?.GetValue(viewModel));
        Assert.Equal(["OpenBackupFolder", "Exit"], actions.Cast<object>().Select(value => value.ToString()));

        var view = Assert.IsAssignableFrom<Control>(Activator.CreateInstance(viewType));
        view.DataContext = viewModel;
        var window = new Window { Width = 840, Height = 560, Content = view };
        window.Show();
        view.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();

        var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.Equal(2, buttons.Length);
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))));
        Assert.DoesNotContain(
            buttons,
            button => string.Concat(
                    AutomationProperties.GetName(button),
                    button.CommandParameter?.ToString())
                .Contains("overwrite", StringComparison.OrdinalIgnoreCase));

        var visibleText = string.Join(
            Environment.NewLine,
            view.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text));
        Assert.Contains(databasePath, visibleText, StringComparison.Ordinal);
        Assert.Contains(backupPath, visibleText, StringComparison.Ordinal);

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var artifactPath = System.IO.Path.Combine(
            GetRepositoryRoot(),
            "artifacts",
            "ui-captures",
            "T4",
            "database-recovery.png");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(artifactPath)!);
        frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
        Assert.True(File.Exists(artifactPath));
        window.Close();
    }

    private static void ApplySpanishResources(Assembly presentation)
    {
        var appType = RequireType(presentation, "ApSolutions.LocalMedia.Presentation.App");
        var method = appType.GetMethod("ApplyLanguage", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.NotNull(Avalonia.Application.Current);
        method.Invoke(null, [Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES")]);
    }

    private static Type RequireType(Assembly assembly, string fullName)
    {
        var type = assembly.GetType(fullName, throwOnError: false);
        Assert.NotNull(type);
        return type;
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
