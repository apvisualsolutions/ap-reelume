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
using Avalonia.Layout;
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
    /// <summary>
    /// The two surfaces on the player that are cards, and therefore have corners.
    /// </summary>
    /// <remarks>
    /// <c>TransportControlsSurface</c> was the third until 2026-08-22, when it stopped being a card:
    /// it was a floating panel with a 16 px margin on all four sides and the picture showed through
    /// underneath it on the left and the right, where the prototype draws a band across the whole
    /// foot. A band meets three edges of the window, and a rounded corner meeting a straight window
    /// edge is a gap. It is asserted below to have <b>no</b> corner rather than dropped from the list
    /// in silence — the second half is what would otherwise rot.
    /// </remarks>
    private static readonly string[] BorderedSurfaces =
        ["PlayerFailureSurface", "AudioAbsenceNotice"];

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

    /// <summary>
    /// The player has a surface of its own, and it is the same in all four themes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other surface in this tree follows the theme; this one does not, because what sits on it
    /// is the picture. That is a decision rather than an oversight, so it is asserted rather than left
    /// to whoever edits the dictionaries next — and it is asserted as <b>four identical
    /// declarations</b>, because a brush that is only right in the variant a test happens to run under
    /// is a brush nobody is watching.
    /// </para>
    /// <para>
    /// Both halves are checked: the markup, which says where the colour comes from, and the painted
    /// value, which says it reached the screen. Neither alone is the assertion.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void The_player_surface_is_its_own_colour_in_all_four_themes()
    {
        var (window, view) = Show();

        var panel = Assert.Single(
            view.GetVisualDescendants().OfType<Panel>(),
            candidate => candidate.Background is ISolidColorBrush);
        Assert.Equal(
            ThemeColour("PlayerSurfaceBrush"),
            Assert.IsAssignableFrom<ISolidColorBrush>(panel.Background).Color);
        Assert.NotEqual(ThemeColour("ShellSurfaceBrush"), ThemeColour("PlayerSurfaceBrush"));

        var tokens = File.ReadAllText(RepositoryLayout.PathFromRoot(
            "src/ApSolutions.LocalMedia.Presentation/Theme/DesignTokens.axaml"));
        var declarations = Regex.Matches(
            tokens,
            @"<SolidColorBrush x:Key=""PlayerSurfaceBrush"" Color=""(?<colour>#[0-9A-Fa-f]{6})"" />",
            RegexOptions.None,
            TimeSpan.FromSeconds(2));

        Assert.Equal(4, declarations.Count);
        Assert.Single(declarations.Select(match => match.Groups["colour"].Value).Distinct(StringComparer.OrdinalIgnoreCase));
        Assert.Equal("#0B0D10", declarations[0].Groups["colour"].Value, ignoreCase: true);

        // And nothing reads against it. ContrastTokenTests measures primary text on every surface it
        // can land on, and this one is deliberately not on that list: everything the player draws over
        // the picture carries a background of its own, so no text is ever read against #0B0D10 in a
        // theme whose text is dark. That is the assertion, because leaving it out of the contrast list
        // with no reason written looks exactly like forgetting to add it.
        var overPicture = panel.Children.OfType<Control>().Where(child => child is not VideoFrameView);
        Assert.NotEmpty(overPicture);
        Assert.All(
            overPicture,
            child => Assert.True(
                child is Border { Background: not null },
                $"{child.GetType().Name} sits over the picture with no surface of its own, so its text "
                + "would be read against the player's background."));

        window.Close();
    }

    /// <summary>
    /// Every panel that floats over the picture is sized to itself, and none of them can stretch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three of these already carry explicit alignment, which was the correction of 2026-08-17 after
    /// the walk found the resume offer drawn at <b>1280×1400</b> over a 1280×1400 stage with its two
    /// buttons in the corner. Alignment stops the stretch in the axis it names; §4 adds the width cap,
    /// which is what keeps a long sentence from making the card as wide as the film.
    /// </para>
    /// <para>
    /// Measured rather than read: each view is mounted alone in a 1280 px window, which is the widest
    /// thing that can contain it, and what is asserted is the width it actually took. A view with no
    /// data context leaves every <c>IsVisible</c> at its default, so every branch is on screen at once
    /// — an upper bound rather than a scene, which is the same trick <c>ViewOverflowTests</c> uses.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void No_panel_over_the_picture_stretches_to_the_stage()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var caps = new (Func<Control> Build, string Surface, double Cap)[]
        {
            (() => new ResumePromptView(), "ResumePromptSurface", 420),
            (() => new NextEpisodeOverlay(), "NextEpisodeSurface", 420),
            (() => new VersionSwitchDialog(), "VersionSwitchSurface", 520),
        };

        foreach (var (build, surfaceName, cap) in caps)
        {
            var view = build();
            var window = new Window { Width = 1280, Height = 800, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // A long sentence is put in on purpose, because that is the only thing the cap protects
            // against: with the short strings these views carry today they do not reach 420 px on their
            // own, so measuring them as they come would pass before anything was capped at all — the
            // false green would be the normal case.
            var line = view.GetVisualDescendants().OfType<TextBlock>().First();
            line.Text = string.Join(' ', Enumerable.Repeat("una frase larguísima que nadie previó", 12));
            Dispatcher.UIThread.RunJobs();
            window.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();

            var surface = Assert.Single(
                view.GetVisualDescendants().OfType<Control>(),
                control => control.Name == surfaceName);
            Assert.True(
                surface.Bounds.Width <= cap,
                $"{surfaceName} took {surface.Bounds.Width:F0} px of a 1280 px stage, past its {cap} px cap.");
            window.Close();
        }

        // The skip button is not a panel, so what it needs is a corner rather than a cap: bottom-right
        // with a margin, out of the way of the transport and of the picture's middle.
        var skip = new SkipMarkerButton();
        var skipWindow = new Window { Width = 1280, Height = 800, Content = skip };
        skipWindow.Show();
        Dispatcher.UIThread.RunJobs();

        var button = Assert.Single(
            skip.GetVisualDescendants().OfType<Control>(),
            control => control.Name == "SkipMarkerButtonControl");
        Assert.Equal(HorizontalAlignment.Right, button.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Bottom, button.VerticalAlignment);
        Assert.Equal(new Thickness(24), button.Margin);
        skipWindow.Close();
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

        // And the band, which is the opposite assertion and the reason it left the list above: it
        // runs to three edges of the window, and only its top edge is drawn.
        var band = view.GetVisualDescendants()
            .OfType<Border>()
            .SingleOrDefault(candidate => candidate.Name == "TransportControlsSurface");
        Assert.True(band is not null, "The transport band is not on the player surface.");
        Assert.Equal(default, band!.CornerRadius);
        Assert.Equal(new Thickness(0, 1, 0, 0), band.BorderThickness);

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
        // With the transport, because the session's three moved into it on 2026-08-25: the prototype
        // puts back, play and forward together, and a player mounted without a transport model now
        // has no play button to measure.
        var (window, view) = Show(withTransport: true);
        var expected = ChromeButtonCorner();

        foreach (var key in TransportChrome)
        {
            var button = view.GetVisualDescendants()
                .OfType<Button>()
                .SingleOrDefault(candidate => KeyOf(candidate) == key);

            Assert.True(button is not null, $"{key} is not on the transport.");
            Assert.Contains("player-chrome", button!.Classes);
            Assert.True(
                button.MinWidth >= 44 && button.MinHeight >= 44,
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

        var (window, view) = Show(withTransport: true);
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

    /// <summary>
    /// The corner a chromed <b>button</b> takes, which is the pill and therefore a circle.
    /// </summary>
    /// <remarks>
    /// It was the medium radius until 2026-08-25, which on a 44 by 44 target is a square with its
    /// corners taken off: «todos los botones o son redondos o son píldoras, pero nunca cuadrados».
    /// The surfaces around them keep the medium radius — a band is not a button.
    /// </remarks>
    private static CornerRadius ChromeButtonCorner()
    {
        var expected = Assert.IsType<CornerRadius>(
            Avalonia.Application.Current!.TryFindResource("CornerRadiusPill", out var token)
                ? token
                : null);
        Assert.True(
            expected.TopLeft > 0,
            "CornerRadiusPill resolved to nothing, so comparing against it proves nothing.");
        return expected;
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
