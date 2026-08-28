// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The speed menu, which was eleven bare numbers in a <c>MenuFlyout</c> until 2026-08-28.
/// </summary>
/// <remarks>
/// <para>
/// The prototype draws nine rows, each with a mark, a name and a note, opening upward, with «Volver a
/// 1×» as a button beside the pill rather than as a twelfth thing inside the list. Every one of those
/// is asserted here <b>off the mounted control</b> and not off the markup: the check this replaced
/// read <c>TransportControlsView.axaml</c> as text and matched <c>CommandParameter="([0-9.]+)"</c>
/// against the policy's steps, which is the shape three suites in this repository have already gone
/// blind in — a file that stops matching the pattern reports an empty list rather than a failure, and
/// an empty list compared against an empty list passes.
/// </para>
/// <para>
/// It is also the seam that keeps <see cref="PlaybackControlPolicy.SpeedSteps"/> honest. That list
/// was read by nothing in <c>src/</c> at all — the menu wrote its own ten numbers — so the policy
/// decided nothing while a comment claimed the keyboard walked it. Now the menu is built from it, and
/// this says so by comparing the two.
/// </para>
/// </remarks>
public sealed class SpeedMenuTests
{
    /// <summary>The drop-down offers the policy's steps, in its order, and nothing else.</summary>
    [AvaloniaFact]
    public void The_menu_offers_the_policys_steps_and_no_others()
    {
        using var scope = Mount();

        Assert.Equal(
            PlaybackControlPolicy.SpeedSteps,
            scope.Menu.Items.Cast<SpeedOption>().Select(option => option.Multiplier));

        // The anti-blindness half: a control with no items would satisfy the comparison above only if
        // the policy were empty too, and this says out loud that neither is.
        Assert.Equal(9, scope.Menu.ItemCount);
        Assert.DoesNotContain(1.75, PlaybackControlPolicy.SpeedSteps);
    }

    /// <summary>
    /// Each row carries the three things the prototype writes on it, and the multiplier alone is not
    /// one of them.
    /// </summary>
    /// <remarks>
    /// The note is the half that would be easiest to leave out and the half that carries the meaning:
    /// <c>0,75×</c> and <c>1,25×</c> are the same distance from normal and read alike until something
    /// says which way each one goes.
    /// </remarks>
    [AvaloniaFact]
    public void Every_row_says_its_multiplier_its_name_and_which_way_it_goes()
    {
        using var scope = Mount();
        var options = scope.Menu.Items.Cast<SpeedOption>().ToArray();

        var normal = Assert.Single(options, option => option.Multiplier == 1.0);
        Assert.Equal("Normal", normal.Label);
        Assert.Equal("1×", normal.Note);
        Assert.Equal("1×", normal.Value);

        Assert.All(
            options.Where(option => option.Multiplier < 1.0),
            option =>
            {
                Assert.Equal("más lenta", option.Note);
                Assert.Equal(option.Value, option.Label);
            });

        Assert.All(
            options.Where(option => option.Multiplier > 1.0),
            option => Assert.Equal("más rápida", option.Note));

        // The multipliers themselves follow the culture in force, which is the whole reason they are
        // formatted rather than written into the markup: a literal «0,25×» would say «0,25×» in
        // English too.
        Assert.Equal("0,25×", options[0].Value);
        Assert.Equal("4×", options[^1].Value);
    }

    /// <summary>
    /// The mark is on the step in force and on no other, and it is the control's own idea of which
    /// row that is.
    /// </summary>
    /// <remarks>
    /// It is the third signal on the row and the only one left in the two high contrasts, where the
    /// selected fill and the resting fill resolve to the same colour. Asserted after opening the
    /// panel, because a container that has never been realised has no visual tree to look at.
    /// </remarks>
    [AvaloniaFact]
    public async Task Only_the_step_in_force_wears_the_mark()
    {
        using var scope = Mount();
        await scope.SetSpeedAsync(1.5);

        scope.Menu.IsDropDownOpen = true;
        Dispatcher.UIThread.RunJobs();

        var marked = new List<double>();
        foreach (var option in scope.Menu.Items.Cast<SpeedOption>())
        {
            if (scope.Menu.ContainerFromItem(option) is not ComboBoxItem container)
            {
                continue;
            }

            var mark = container.GetVisualDescendants()
                .OfType<TextBlock>()
                .SingleOrDefault(block => block.Classes.Contains("speed-mark"));
            Assert.NotNull(mark);
            Assert.Equal("●", mark!.Text);
            if (mark.IsVisible)
            {
                marked.Add(option.Multiplier);
            }
        }

        // The realisation floor: an unrealised list would leave `marked` empty and pass a check that
        // only asked for "no more than one".
        Assert.Equal([1.5], marked);
        scope.Menu.IsDropDownOpen = false;
    }

    /// <summary>The panel opens upward, which no other drop-down in this application does.</summary>
    /// <remarks>
    /// The transport is the bottom edge of the player, so a panel dropping from it opens off the
    /// screen. The prototype writes <c>bottom:38px</c>; this is the same statement made of the part
    /// the class overrides.
    /// </remarks>
    [AvaloniaFact]
    public void The_panel_opens_upward_and_the_other_drop_downs_do_not()
    {
        using var scope = Mount();

        Assert.Equal(PlacementMode.Top, Popup(scope.Menu).Placement);
    }

    /// <summary>
    /// The closed pill says the multiplier where the open row says the word for it.
    /// </summary>
    /// <remarks>
    /// Both faces are drawn from the same step, so one of them has to be told to draw it differently
    /// — and the one that is told is the closed one, through a null <c>ContentTemplate</c> and
    /// <see cref="SpeedOption.ToString"/>. Asserted as the text a person reads, not as the setter,
    /// because a template redirect that resolved to nothing would leave the pill blank and satisfy
    /// any check that only asked whether the redirect was there.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_closed_pill_says_the_multiplier_and_the_row_says_the_word()
    {
        using var scope = Mount();
        await scope.SetSpeedAsync(1.0);

        var face = scope.Menu.GetVisualDescendants()
            .OfType<ContentControl>()
            .Single(control => control.Name == "ContentPresenter");

        Assert.Null(face.ContentTemplate);
        var painted = face.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToArray();
        Assert.Contains("1×", painted);
        Assert.DoesNotContain("Normal", painted);
    }

    /// <summary>Choosing a row reaches the engine that is playing.</summary>
    /// <remarks>
    /// This is the half the readout never had: before the menu arrived the speed could only be
    /// changed from the keyboard, and a pointer could watch the number and do nothing to it. The
    /// probe is the engine's own speed rather than the row's highlight, for the reason the volume
    /// slider taught — a control that moved its own thumb and left the session where it was is
    /// exactly the state that slider was in for four months.
    /// </remarks>
    [AvaloniaFact]
    public async Task Choosing_a_row_reaches_the_engine()
    {
        using var scope = Mount();

        scope.Menu.SelectedValue = 2.0;
        Dispatcher.UIThread.RunJobs();
        await WaitForAsync(() => scope.Engine.LastSpeed == 2.0, "choosing 2× never reached the engine");

        Assert.Equal(2.0, scope.Model.SpeedMultiplier);

        // A list with nothing chosen sends no speed. It is the state the pill is in while its items
        // are being replaced, and a null there would be asked to be a multiplier.
        scope.Engine.LastSpeed = null;
        scope.Menu.SelectedValue = null;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(scope.Engine.LastSpeed);

        // And choosing again the step the session is already playing at reaches nothing. This is the
        // arm that keeps the handler from looping: applying the engine's answer raises
        // SpeedMultiplier, the one-way binding puts the row back, and that raises this same event
        // with the value the model already holds. Reached from null rather than from 2× itself,
        // because setting a list to what it already holds raises no event at all.
        scope.Menu.SelectedValue = 2.0;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(scope.Engine.LastSpeed);

        // And a bar with no session behind it yet, which is the state between the view being built
        // and the composition filling it. A selection there reaches nothing rather than throwing.
        scope.Transport.DataContext = null;
        Dispatcher.UIThread.RunJobs();
        scope.Menu.SelectedValue = 0.5;
        Dispatcher.UIThread.RunJobs();
        Assert.Null(scope.Engine.LastSpeed);
    }

    /// <summary>«Volver a 1×» is on the bar only while there is something to come back from.</summary>
    /// <remarks>
    /// It was the menu's eleventh row, which put a thing that is not a speed in a list of speeds and
    /// hid it behind the click that opens that list. The prototype puts it beside the pill.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_reset_is_beside_the_pill_and_only_while_it_has_work()
    {
        using var scope = Mount();
        var reset = scope.Transport.GetVisualDescendants()
            .OfType<Button>()
            .Single(button => button.Name == "SpeedResetButton");

        Assert.False(reset.IsVisible);
        Assert.Equal(
            Assert.IsType<string>(Avalonia.Application.Current!.FindResource("TransportSpeedResetAction")),
            AutomationProperties.GetName(reset));

        await scope.SetSpeedAsync(0.5);
        Dispatcher.UIThread.RunJobs();
        Assert.True(reset.IsVisible);

        reset.Command!.Execute(reset.CommandParameter);
        await WaitForAsync(() => scope.Engine.LastSpeed == 1.0, "the reset never brought the session back to 1×");
        Dispatcher.UIThread.RunJobs();
        Assert.False(reset.IsVisible);
    }

    private static Popup Popup(ComboBox menu) =>
        menu.GetVisualDescendants().OfType<Popup>().Single(popup => popup.Name == "PART_Popup");

    private static async Task WaitForAsync(Func<bool> condition, string complaint)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(condition(), complaint);
    }

    private static Scope Mount()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        return new Scope();
    }

    /// <summary>
    /// The transport in a window, with a session behind it whose speed can be read back.
    /// </summary>
    /// <remarks>
    /// Wide on purpose: the row wraps, and a narrow window would put the pill on a second line where
    /// it still measures and still answers, which is a state this suite has nothing to say about.
    /// </remarks>
    private sealed class Scope : IDisposable
    {
        private readonly Window _window;

        internal Scope()
        {
            Engine = new RecordingEngine();
            Model = new TransportControlsViewModel(new ControlPlayback(Engine));
            Transport = new TransportControlsView { DataContext = Model };
            _window = new Window { Width = 1200, Height = 400, Content = Transport };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
            Menu = Transport.GetVisualDescendants().OfType<ComboBox>().Single(box => box.Name == "SpeedReadout");
        }

        internal RecordingEngine Engine { get; }

        internal TransportControlsViewModel Model { get; }

        internal TransportControlsView Transport { get; }

        internal ComboBox Menu { get; }

        internal async Task SetSpeedAsync(double multiplier)
        {
            await Model.SetSpeedAsync(multiplier, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose() => _window.Close();
    }

    /// <summary>An engine that plays nothing and remembers the last speed it was asked for.</summary>
    internal sealed class RecordingEngine : IMediaPlayerEngine
    {
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

        public PlaybackState State => PlaybackState.Playing;

        public double? LastSpeed { get; set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(PlaybackState.Playing, TimeSpan.Zero, null, []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default)
        {
            LastSpeed = multiplier;
            return Task.CompletedTask;
        }

        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
