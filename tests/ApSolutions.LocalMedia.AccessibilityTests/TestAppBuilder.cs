// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(ApSolutions.LocalMedia.AccessibilityTests.TestAppBuilder))]

namespace ApSolutions.LocalMedia.AccessibilityTests;

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
