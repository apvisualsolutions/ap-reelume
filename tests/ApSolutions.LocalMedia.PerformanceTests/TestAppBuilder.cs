using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(ApSolutions.LocalMedia.PerformanceTests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ApSolutions.LocalMedia.PerformanceTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessPerformanceApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}

internal sealed class HeadlessPerformanceApplication : Avalonia.Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        var assetRoot = new Uri("avares://ApSolutions.LocalMedia.Presentation/");
        Styles.Add(new StyleInclude(assetRoot)
        {
            Source = new Uri($"{assetRoot}Theme/DesignTokens.axaml"),
        });
    }
}
