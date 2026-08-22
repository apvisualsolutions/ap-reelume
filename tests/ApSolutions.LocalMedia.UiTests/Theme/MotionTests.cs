// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Animation;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// The animations exist, they all read one duration, and that duration is one somebody can switch
/// off.
/// </summary>
/// <remarks>
/// <para>
/// The package asks for four — <c>apr-in</c>, <c>apr-shim</c>, <c>apr-tip</c>, <c>apr-pulse</c> — and
/// two of them are here. The other two are <b>not deferred, they are answered</b>, and the answers
/// are measurements rather than schedule:
/// </para>
/// <list type="bullet">
/// <item><c>apr-shim</c> is the shimmer over a skeleton <em>while a list loads</em>, and nothing in
/// this application knows it is loading. <c>ReviewInboxView</c>'s own audit measured that in August:
/// no view model carries a loading state, so the skeleton has nothing to be a skeleton of. It
/// arrives with the first read model that reports one, not before.</item>
/// <item><c>apr-in</c> is the six-pixel rise on every screen change, and the shell does not change
/// screens: it mounts all eleven and toggles <c>IsVisible</c>, which is a property Avalonia does not
/// animate — the control is not rendered while it is false, so there is no frame to start from.
/// Getting it needs the shell rebuilt around one <c>ContentControl</c> whose content is replaced,
/// which is a change to how every surface in the application is hosted and not a line of markup.</item>
/// </list>
/// <para>
/// What is asserted about the two that exist is the <b>contract</b>, not the pixels: every animation
/// in the tree takes its duration from the one token, and the theme service writes zero into that
/// token when Windows asks for less motion. A test that watched an animation play would be timing a
/// render on a shared runner, which is the shape of a flake.
/// </para>
/// </remarks>
public sealed class MotionTests
{
    private const string MotionDurationKey = "MotionDuration";

    /// <summary>Every animation in the tree reads the token, and there is at least one to read it.</summary>
    [Fact]
    public void Every_animation_takes_its_duration_from_the_one_token()
    {
        var animations = Directory
            .EnumerateFiles(
                Path.Combine(RepositoryLayout.Root, "src"),
                "*.axaml",
                SearchOption.AllDirectories)
            .SelectMany(path => XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName == "Animation")
                .Select(element => (View: Path.GetFileName(path), Element: element)))
            .ToArray();

        // Anti-blindness floor: this whole file passes by measuring nothing the day the animations
        // are removed, and "no animations" is exactly the state it was written to leave behind.
        Assert.True(
            animations.Length >= 2,
            $"only {animations.Length} animations were found under src/, and two were declared.");

        var handWritten = animations
            .Where(entry => entry.Element.Attribute("Duration")?.Value.TrimStart().StartsWith('{') != true)
            .Select(entry => $"{entry.View}: Duration=\"{entry.Element.Attribute("Duration")?.Value}\"")
            .ToArray();
        Assert.True(
            handWritten.Length == 0,
            "an animation writes its own duration, so reduced motion cannot reach it:\n  "
                + string.Join("\n  ", handWritten));
    }

    /// <summary>
    /// The token is declared as a real duration, and the animations resolve it.
    /// </summary>
    /// <remarks>
    /// Resolved through the application rather than read out of the file, because a token that
    /// parses and a token that <em>reaches an animation</em> are different claims, and the second is
    /// the one that matters. A <c>DynamicResource</c> inside a <c>Style</c> has no data context and
    /// no logical parent of its own; that it resolves at all is the thing worth proving.
    /// </remarks>
    [AvaloniaFact]
    public void The_token_is_a_duration_and_the_animations_resolve_it()
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        Assert.True(
            application.TryGetResource(MotionDurationKey, application.ActualThemeVariant, out var declared),
            "MotionDuration is not declared, so no animation has a duration to read.");
        Assert.Equal(TimeSpan.FromMilliseconds(160), Assert.IsType<TimeSpan>(declared));

        var animated = application.Styles
            .SelectMany(Flatten)
            .SelectMany(style => style.Animations)
            .OfType<Animation>()
            .ToArray();
        Assert.True(
            animated.Length >= 2,
            $"the merged theme carries {animated.Length} animations, and two were declared.");
        Assert.All(
            animated,
            animation => Assert.NotEqual(default, animation.Duration));
    }

    /// <summary>
    /// Every style in a tree of them, through the include that loads the token file.
    /// </summary>
    /// <remarks>
    /// The <c>StyleInclude</c> case is not defensive: the theme reaches the application as an
    /// include, so a walk that only knew about <c>Style</c> and <c>Styles</c> found <b>zero</b>
    /// animations and would have agreed with an application that had none.
    /// </remarks>
    private static IEnumerable<Style> Flatten(IStyle style) => style switch
    {
        Style single => [single, .. single.Children.SelectMany(Flatten)],
        Styles many => many.SelectMany(Flatten),
        StyleInclude include when include.Loaded is { } loaded => Flatten(loaded),
        _ => [],
    };
}
