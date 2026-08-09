using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(ApSolutions.LocalMedia.IntegrationTests.TestAppBuilder))]

namespace ApSolutions.LocalMedia.IntegrationTests;

/// <summary>
/// A headless Avalonia application, needed by the few integration tests that exercise a real
/// Windows adapter built on Avalonia types, such as the notification-area icon.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

internal sealed class HeadlessTestApplication : Avalonia.Application
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
