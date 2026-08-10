// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(ApSolutions.LocalMedia.UiTests.TestAppBuilder))]

namespace ApSolutions.LocalMedia.UiTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<HeadlessTestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
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
