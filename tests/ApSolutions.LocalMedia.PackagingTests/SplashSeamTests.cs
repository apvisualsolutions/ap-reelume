// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The seam between what Windows paints before the application starts and what the application paints
/// first.
/// </summary>
/// <remarks>
/// <para>
/// The package manifest's <c>BackgroundColor</c> is what Windows shows while the process starts, and
/// <c>StartupView</c> is the first thing the application itself draws — over
/// <c>ShellSurfaceBrush</c>. §4 asks for the two to be the same colour so the join does not show, and
/// today they are: both <c>#111827</c>. Nothing was watching that, so it was one edit away from
/// becoming a flash on every launch.
/// </para>
/// <para>
/// <b>It can only match one theme, and that is a decision rather than an oversight.</b> A manifest
/// colour is static — Windows paints it before any code of ours runs, so it cannot know which theme
/// the person chose — while <c>ShellSurfaceBrush</c> is one of four. It is matched to <b>Dark</b>,
/// which is the variant the design package builds around and the one the player lives in. Picking a
/// colour that offends none of the four would be a mid grey that suits none of them either.
/// </para>
/// </remarks>
public sealed class SplashSeamTests
{
    private const string DarkDictionary = "Dark";

    private const string ShellSurface = "ShellSurfaceBrush";

    [Fact]
    public void The_splash_and_the_first_screen_the_application_paints_are_the_same_colour()
    {
        var manifest = PackageEvidence.Manifest();
        var declared = manifest
            .Descendants()
            .Select(element => element.Attribute("BackgroundColor")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            declared.Length == 1,
            $"The manifest declares {declared.Length} background colours ({string.Join(", ", declared)}); "
                + "the splash and the tile have to agree before either can match the application.");

        var surface = DarkShellSurface();
        Assert.Equal(surface, declared[0], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What the dark theme paints the shell with, read from the dictionary the application actually
    /// uses rather than from a copy kept here.
    /// </summary>
    private static string DarkShellSurface()
    {
        var xaml = XDocument.Load(Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Theme",
            "DesignTokens.axaml"));

        var key = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        var dark = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary")
            .FirstOrDefault(element => element.Attribute(key)?.Value == DarkDictionary);

        Assert.True(dark is not null, $"No {DarkDictionary} dictionary, so this compares nothing.");

        var brush = dark!
            .Descendants()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .FirstOrDefault(element => element.Attribute(key)?.Value == ShellSurface);

        Assert.True(brush is not null, $"{DarkDictionary} declares no {ShellSurface}.");
        var colour = brush!.Attribute("Color")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(colour), $"{ShellSurface} carries no colour.");
        return colour!;
    }
}
