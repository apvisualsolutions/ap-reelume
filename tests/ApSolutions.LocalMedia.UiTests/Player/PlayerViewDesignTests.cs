// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// What the redesign asks of the player surface: one primary action on the screen that has one,
/// corners from the token rather than from a number written twice, and the mini player's chrome
/// class generalised to the large transport.
/// </summary>
/// <remarks>
/// <para>
/// The view is mounted without a data context on purpose, and that is not laziness: none of these
/// questions depends on one — a class is a class and a corner radius is a corner radius — and a
/// binding that resolves to nothing leaves <c>IsVisible</c> at its default, so every branch of this
/// view is on screen at once. That is the widest the row can ever be, which is the case worth
/// measuring.
/// </para>
/// <para>
/// The five buttons carry no <c>x:Name</c>, because the walk aims at the resource key behind the
/// accessible name and that is what the inventory counts. So they are found here the same way: by
/// the name the theme resolved, not by a string this file carries a copy of.
/// </para>
/// </remarks>
public sealed class PlayerViewDesignTests
{
    /// <summary>The one action of the one screen here that is for an action.</summary>
    private static readonly string[] LeadingAction = ["PlayerRecoveryRetry"];

    /// <summary>The three surfaces the redesign gives a corner to.</summary>
    private static readonly string[] BorderedSurfaces =
        ["PlayerFailureSurface", "AudioAbsenceNotice", "TransportControlsSurface"];

    /// <summary>The large transport's own three, which take the chrome the mini player defined.</summary>
    private static readonly string[] TransportChrome =
        ["PlayerPlayAction", "PlayerPauseAction", "PlayerStopAction"];

    /// <summary>
    /// Retry leads the failure screen, and nothing leads the transport.
    /// </summary>
    /// <remarks>
    /// The primary action is asserted as the <em>only</em> one rather than merely as present.
    /// <c>Play</c> and <c>Pause</c> alternate <b>by state</b>, so marking either would make the
    /// screen change what it is for depending on what is happening — which is the one thing a
    /// hierarchy cannot do — and <c>Stop</c> is the point of nothing. Two primary actions would pass
    /// an assertion that only looked for one.
    /// </remarks>
    [AvaloniaFact]
    public void Exactly_one_button_leads_the_player_screen()
    {
        var (window, view) = Show();

        var leading = view.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("primary-action"))
            .Select(KeyOf)
            .ToArray();

        Assert.Equal(LeadingAction, leading);
        window.Close();
    }

    /// <summary>
    /// A session that will not open is a failure, and it says so with a surface and a glyph of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to wear <c>ShellSurfaceBrush</c> — the same surface as everything else the shell draws
    /// — so the one screen that has to say "this did not work" looked exactly like the one that says
    /// what codec is in use. §4 gives it <c>DangerSurfaceBrush</c> with a border and a glyph.
    /// </para>
    /// <para>
    /// The glyph is asserted to <b>differ from the warning's</b>, which is the whole point of having
    /// one: a failure and a notice that shared a glyph and differed only in colour would be telling
    /// them apart by colour alone, which is the thing this redesign spends its whole grammar avoiding.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void A_failure_wears_the_danger_surface_and_a_glyph_that_is_not_the_warning_one()
    {
        var (window, view) = Show();

        var failure = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "PlayerFailureSurface");
        Assert.Equal(
            ThemeColour("DangerSurfaceBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(failure.Background).Color);
        Assert.Equal(
            ThemeColour("DangerBorderBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(failure.BorderBrush).Color);
        Assert.NotEqual(ThemeColour("DangerSurfaceBrush"), ThemeColour("ShellSurfaceBrush"));

        var glyph = Assert.Single(
            failure.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Text == "\u2715");
        Assert.NotNull(glyph);

        window.Close();
    }

    /// <summary>A theme brush's colour, asked for by the variant in force.</summary>
    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme variant.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    /// <summary>
    /// All three bordered surfaces take their corner from the theme, and the markup says so.
    /// </summary>
    /// <remarks>
    /// The source is what is asserted, not only the painted value, and that distinction is the whole
    /// test. <c>CornerRadiusMedium</c> is 8 and the literals here were 8, so a comparison of painted
    /// numbers would pass <b>before</b> the view is changed at all — the false green is the normal
    /// case, not the unlucky one. The painted value is asserted too, because markup that names a
    /// resource proves nothing about what reached the screen, and the token is resolved rather than
    /// written down: a copy of 8 in here would agree with itself while the theme said something else.
    /// </remarks>
    [AvaloniaFact]
    public void The_bordered_surfaces_take_their_corner_from_the_theme()
    {
        var (window, view) = Show();
        var expected = ChromeCorner();

        var markup = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Player/PlayerView.axaml"));
        var literals = Regex.Matches(markup, @"CornerRadius=""[0-9]", RegexOptions.None, TimeSpan.FromSeconds(2));
        Assert.True(
            literals.Count == 0,
            $"PlayerView.axaml still writes {literals.Count} corner radius as a number. A number "
                + "written beside a token is a number that will disagree with it.");

        foreach (var name in BorderedSurfaces)
        {
            var border = view.GetVisualDescendants()
                .OfType<Border>()
                .SingleOrDefault(candidate => candidate.Name == name);

            Assert.True(border is not null, $"{name} is not on the player surface.");
            Assert.Equal(expected, border!.CornerRadius);
        }

        window.Close();
    }

    /// <summary>
    /// The three transport buttons wear the chrome class, and it reaches the element that paints.
    /// </summary>
    /// <remarks>
    /// The minimum target area is the point of generalising the class: it is an accessibility gain
    /// and not a layout one. The corner is read off the presenter because a setter on a
    /// <c>Button</c> is not the same as a setter on what draws it — measured in phase 2a, where a
    /// <c>Background</c> on the button lost to the base theme outright.
    /// </remarks>
    [AvaloniaFact]
    public void The_large_transport_wears_the_chrome_the_mini_player_defined()
    {
        var (window, view) = Show();
        var expected = ChromeCorner();

        foreach (var key in TransportChrome)
        {
            var button = view.GetVisualDescendants()
                .OfType<Button>()
                .SingleOrDefault(candidate => KeyOf(candidate) == key);

            Assert.True(button is not null, $"{key} is not on the transport.");
            Assert.Contains("player-chrome", button!.Classes);
            Assert.True(
                button.MinWidth >= 36 && button.MinHeight >= 36,
                $"{key} is smaller than the target area: {button.MinWidth}x{button.MinHeight}.");

            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().FirstOrDefault();
            Assert.True(presenter is not null, $"{key} has no presenter, so nothing painted it.");
            Assert.Equal(expected, presenter!.CornerRadius);
        }

        window.Close();
    }

    /// <summary>
    /// The chrome class carries no margin, because a margin belongs to whoever places the control.
    /// </summary>
    /// <remarks>
    /// The class was written for the mini player, where four on every side was the separation. The
    /// large transport places its three in a <c>StackPanel</c> that already spaces them, so the same
    /// class would add four a side on top and push them twenty apart. What is the control's is the
    /// target area and the corner; where it sits is its parent's. The theme file is read as text
    /// because a margin of zero is also what a control with no class at all reports, so the painted
    /// value alone cannot tell a setter that was removed from one that never applied.
    /// </remarks>
    [AvaloniaFact]
    public void The_chrome_class_carries_no_margin_of_its_own()
    {
        var tokens = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));
        var chrome = Regex.Match(
            tokens,
            @"<Style Selector=""Button\.player-chrome"">(?<body>.*?)</Style>",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(2));

        Assert.True(chrome.Success, "Button.player-chrome is not declared, so this measures nothing.");
        Assert.DoesNotContain("Property=\"Margin\"", chrome.Groups["body"].Value, StringComparison.Ordinal);

        var (window, view) = Show();
        foreach (var key in TransportChrome)
        {
            var button = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => KeyOf(candidate) == key);

            Assert.Equal(default, button.Margin);
        }

        window.Close();
    }

    /// <summary>
    /// Every transport control stays inside the window, at the narrowest the application allows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 900 is not a round number picked for the test: it is <c>MinWidth</c> on the main window in
    /// <c>App.axaml.cs</c>, so it is the narrowest anybody can make this screen. A horizontal
    /// <c>StackPanel</c> holding buttons with translated labels is exactly the shape that has drawn
    /// a control outside the window six times in this repository, and giving those three buttons a
    /// minimum target area makes the row wider still. Measured on 2026-08-20 <b>before</b> deciding
    /// anything: it ended at x=974, which is 74 past the edge, with the transport's own view, its
    /// mute button, its speed readout and its volume slider all outside. The panel became a
    /// <c>WrapPanel</c>, which is the fix the other six got.
    /// </para>
    /// <para>
    /// It is measured here rather than left to the walk because a control off the side is a control
    /// nobody can press, and the failure that produces names the click rather than the layout. Every
    /// state is on screen at once, which is wider than the application can ever be — the five state
    /// labels are mutually exclusive and so are Play and Pause — so this is an upper bound and not a
    /// scene. The transport's own view goes into the host through the content template, which is the
    /// path the application uses, so what is measured is the row a person sees and not a shorter one
    /// with an empty placeholder in it.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_transport_row_stays_inside_the_window()
    {
        var (window, view) = Show(width: 900, height: 640, withTransport: true);

        var transport = view.GetVisualDescendants()
            .OfType<Border>()
            .Single(border => border.Name == "TransportControlsSurface");
        Assert.True(
            transport.Bounds.Width > 0,
            "The transport surface measured to nothing, so its bounds prove nothing.");

        var offside = transport.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Bounds.Width > 0 && control.IsEffectivelyVisible)
            .Select(control => (Control: control, Point: control.TranslatePoint(
                new Point(control.Bounds.Width, control.Bounds.Height),
                window)))
            .Where(measured => measured.Point is { } corner
                && (corner.X > window.Width || corner.Y > window.Height || corner.X < 0))
            .Select(measured => $"{Describe(measured.Control)} ends at {measured.Point}")
            .ToArray();

        Assert.True(
            offside.Length == 0,
            $"{offside.Length} transport control(s) fall outside a {window.Width}x{window.Height} "
                + $"window: {string.Join("; ", offside)}");
        window.Close();
    }

    /// <summary>The corner every chromed surface of the player takes, resolved rather than copied.</summary>
    private static CornerRadius ChromeCorner()
    {
        var expected = Assert.IsType<CornerRadius>(
            Avalonia.Application.Current!.TryFindResource("CornerRadiusMedium", out var token)
                ? token
                : null);
        Assert.True(
            expected.TopLeft > 0,
            "CornerRadiusMedium resolved to nothing, so comparing against it proves nothing.");
        return expected;
    }

    /// <summary>
    /// The resource key a control is declared under, recovered from the name the theme resolved.
    /// </summary>
    /// <remarks>
    /// This is the identity the walk uses and the one the inventory counts, so a test that asked
    /// about <c>x:Name</c> would be asking about a different control than the gate does.
    /// </remarks>
    private static string KeyOf(Control control)
    {
        var name = AutomationProperties.GetName(control);
        foreach (var key in LeadingAction.Concat(TransportChrome).Concat(["PlayerRecoveryOpenExternally"]))
        {
            if (Avalonia.Application.Current!.TryFindResource(key, out var value)
                && string.Equals(value as string, name, StringComparison.Ordinal))
            {
                return key;
            }
        }

        return name ?? "<unnamed>";
    }

    private static string Describe(Control control) =>
        control.Name ?? AutomationProperties.GetName(control) ?? control.GetType().Name;

    private static (Window Window, PlayerView View) Show(
        double width = 900,
        double height = 700,
        bool withTransport = false)
    {
        // A language is applied because the controls here are named by resource key and nothing else:
        // with no dictionary loaded the accessible name resolves to nothing and every lookup below
        // would be asking about a control that has no identity rather than about the wrong one.
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var view = new PlayerView();
        var window = new Window { Width = width, Height = height, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        if (withTransport)
        {
            var host = view.FindControl<ContentControl>("TransportControlsHost")
                ?? throw new InvalidOperationException("The player declares no transport host.");
            host.Content = new TransportControlsViewModel(new ControlPlayback(new StubEngine()));
            Dispatcher.UIThread.RunJobs();
        }

        return (window, view);
    }

    private sealed class StubEngine : IMediaPlayerEngine
    {
        public PlaybackState State => PlaybackState.Idle;

        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<PlaybackFailureEventArgs>? Failure
        {
            add { }
            remove { }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(PlaybackState.Idle, TimeSpan.Zero, null, []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
