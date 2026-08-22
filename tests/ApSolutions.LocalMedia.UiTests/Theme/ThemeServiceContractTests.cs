// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Theme;

/// <summary>
/// What the theme service does when it is handed something wrong, and the one method nothing asked.
/// </summary>
/// <remarks>
/// <para>
/// <c>ThemeTests</c> exercises the service through the settings file it really writes, which is what
/// makes it the right place for the happy path and the wrong place for this: every one of these needs
/// a dependency that misbehaves, and reaching them through reflection wraps the very exception being
/// asserted in a <see cref="System.Reflection.TargetInvocationException"/>.
/// </para>
/// <para>
/// It exists because CI measured this file falling from 90/69 to 90/66 while <b>not one line of it
/// changed</b> — the coverage came from tests that moved on 2026-08-22, and a file whose guarantee is
/// a side effect of somebody else's test has no guarantee. These aim at the decisions: five
/// dependencies that may not be absent, a preference that is not one of the three, a stored setting
/// that has rotted, and the backdrop, which was reached by nothing at all.
/// </para>
/// </remarks>
[Collection("ThemeVariant")]
public sealed class ThemeServiceContractTests
{
    [AvaloniaFact]
    public void None_of_the_five_dependencies_may_be_absent()
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        var store = new StubSettingsStore(ThemePreference.System);
        var backdrop = new StubBackdrop(answer: true);
        var motion = new StubFlag(false);
        var contrast = new StubContrast(enabled: false, isLight: true);

        Assert.Throws<ArgumentNullException>(
            () => new FluentThemeService(null!, store, backdrop, motion, contrast));
        Assert.Throws<ArgumentNullException>(
            () => new FluentThemeService(application, null!, backdrop, motion, contrast));
        Assert.Throws<ArgumentNullException>(
            () => new FluentThemeService(application, store, null!, motion, contrast));
        Assert.Throws<ArgumentNullException>(
            () => new FluentThemeService(application, store, backdrop, null!, contrast));
        Assert.Throws<ArgumentNullException>(
            () => new FluentThemeService(application, store, backdrop, motion, null!));
    }

    /// <summary>
    /// A preference that is not one of the three is refused, and a <em>stored</em> one that is not is
    /// forgiven — which is the right way round.
    /// </summary>
    /// <remarks>
    /// Refusing the stored value would mean a settings file somebody hand-edited, or one written by a
    /// version that had a fourth preference, stops the application from starting. Refusing the
    /// argument is different: that is this process calling its own service wrong, and it is a defect
    /// rather than a state to recover from.
    /// </remarks>
    [AvaloniaFact]
    public void An_impossible_preference_is_refused_as_an_argument_and_forgiven_when_it_was_stored()
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        var service = new FluentThemeService(
            application,
            new StubSettingsStore(ThemePreference.Dark),
            new StubBackdrop(answer: true),
            new StubFlag(false),
            new StubContrast(enabled: false, isLight: true));

        Assert.Equal(ThemePreference.Dark, service.CurrentPreference);
        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => service.Apply((ThemePreference)42));
        Assert.Equal("preference", refused.ParamName);

        // And the stored value nobody can have chosen: the service starts on the system theme rather
        // than refusing to start.
        var rotted = new FluentThemeService(
            application,
            new StubSettingsStore((ThemePreference)42),
            new StubBackdrop(answer: true),
            new StubFlag(false),
            new StubContrast(enabled: false, isLight: true));
        Assert.Equal(ThemePreference.System, rotted.CurrentPreference);
        Assert.Equal(ThemeVariant.Default, application.RequestedThemeVariant);
    }

    /// <summary>
    /// The backdrop is asked of the platform and its answer is passed through, both ways.
    /// </summary>
    /// <remarks>
    /// Both ways matters: Mica is a Windows 11 feature and the answer on a machine without it is
    /// <c>false</c>, which is a normal outcome rather than a failure. A test that only ever saw
    /// <c>true</c> would not notice the day this started answering it unconditionally.
    /// </remarks>
    [AvaloniaFact]
    public void The_backdrop_is_the_platform_answer_and_the_window_is_required()
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        var granted = new StubBackdrop(answer: true);
        var service = new FluentThemeService(
            application,
            new StubSettingsStore(ThemePreference.System),
            granted,
            new StubFlag(false),
            new StubContrast(enabled: false, isLight: true));

        var window = new Window();
        Assert.True(service.TryApplyBackdrop(window));
        Assert.Same(window, granted.Asked);
        Assert.Throws<ArgumentNullException>(() => service.TryApplyBackdrop(null!));

        var refused = new FluentThemeService(
            application,
            new StubSettingsStore(ThemePreference.System),
            new StubBackdrop(answer: false),
            new StubFlag(false),
            new StubContrast(enabled: false, isLight: true));
        Assert.False(refused.TryApplyBackdrop(window));
        window.Close();
    }

    private sealed class StubSettingsStore(ThemePreference stored) : ISettingsStore
    {
        public T? Read<T>(string key) => stored is T typed ? typed : default;

        public void Write<T>(string key, T value)
        {
        }
    }

    private sealed class StubBackdrop(bool answer) : IBackdropService
    {
        public Window? Asked { get; private set; }

        public bool TryApply(Window window)
        {
            Asked = window;
            return answer;
        }
    }

    private sealed class StubFlag(bool isEnabled) : IReducedMotionService
    {
        public bool IsEnabled => isEnabled;
    }

    private sealed class StubContrast(bool enabled, bool isLight) : IHighContrastService
    {
        public bool IsEnabled => enabled;

        public bool IsLight => isLight;
    }
}
