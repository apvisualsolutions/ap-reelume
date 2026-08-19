// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// What a person reads when the player offers them another version, and what happens when the data
/// behind it is incomplete.
/// </summary>
/// <remarks>
/// TST-001's debt. <c>VersionSwitchWiringTests</c> covers the wiring — that a row hands its own
/// version to the use case and that an unavailable one cannot be switched to — and left the label
/// itself untouched, which is where every branch in this file lives. The gap was invisible because
/// the coverage gate only held files that were new, so this one shipped at 45% of its lines and got
/// worse in ARQ-004 without anything noticing. It is a watched file now.
/// </remarks>
public sealed class PlayerVersionsViewModelTests
{
    [Fact]
    public void A_version_that_knows_everything_about_itself_says_all_three_things()
    {
        var row = new PlayerVersionRowViewModel(Version(), Question(), _ => Task.CompletedTask);

        Assert.Equal("3840×2160 · HEVC · HDR", row.QualityLabel);
    }

    /// <summary>
    /// Resolution is one fact, not two: a file that reports a width and no height cannot be
    /// described by either, so the label leaves it out rather than inventing half of it.
    /// </summary>
    [Theory]
    [InlineData(3840, null)]
    [InlineData(null, 2160)]
    [InlineData(null, null)]
    public void Half_a_resolution_is_not_a_resolution(int? width, int? height)
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { Width = width, Height = height },
            Question(),
            _ => Task.CompletedTask);

        Assert.Equal("HEVC · HDR", row.QualityLabel);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_codec_nobody_recorded_is_left_out_instead_of_shown_blank(string codec)
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { VideoCodec = codec },
            Question(),
            _ => Task.CompletedTask);

        Assert.Equal("3840×2160 · HDR", row.QualityLabel);
    }

    [Fact]
    public void Standard_range_is_said_by_saying_nothing()
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { IsHdr = false },
            Question(),
            _ => Task.CompletedTask);

        Assert.Equal("3840×2160 · HEVC", row.QualityLabel);
    }

    /// <summary>
    /// A version the catalogue knows nothing about beyond its existence. An empty label is the
    /// honest answer, and it must not be a row of separators with nothing between them.
    /// </summary>
    [Fact]
    public void A_version_that_knows_nothing_about_itself_says_nothing()
    {
        var row = new PlayerVersionRowViewModel(
            Version() with { Width = null, Height = null, VideoCodec = "", IsHdr = false },
            Question(),
            _ => Task.CompletedTask);

        Assert.Equal(string.Empty, row.QualityLabel);
    }

    [Fact]
    public void A_row_carries_the_version_it_was_built_from()
    {
        var version = Version();

        var row = new PlayerVersionRowViewModel(version, Question(), _ => Task.CompletedTask);

        Assert.Same(version, row.Version);
        Assert.True(row.IsAvailable);
    }

    [Fact]
    public void A_row_with_no_version_and_a_row_with_nowhere_to_switch_are_both_refused()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PlayerVersionRowViewModel(null!, Question(), _ => Task.CompletedTask));
        Assert.Throws<ArgumentNullException>(() =>
            new PlayerVersionRowViewModel(Version(), Question(), null!));

        // The question is mandatory rather than an optional left at null, because a row that does
        // not know about it is exactly the row this file measures: pressable underneath it.
        Assert.Throws<ArgumentNullException>(() =>
            new PlayerVersionRowViewModel(Version(), null!, _ => Task.CompletedTask));
    }

    /// <summary>
    /// A switch already under way is not asked for again, which is what CI measured on 2026-08-18.
    /// </summary>
    /// <remarks>
    /// The walk failed on a hosted runner with "ConfirmSwitchButton is on screen but cannot be
    /// pressed: visible=False, enabled=True" — the dialogue vanished between the harness resolving
    /// the button and pressing it. The cause is here: the row's command stayed pressable while its
    /// own work was in flight, so a second press — a double click, or a harness repeating a press
    /// that seemed to do nothing — started a second switch. And a second switch flushes the playhead
    /// before it decides: a session whose demuxer has not applied its start position yet answers
    /// zero, zero is below the resume floor, so the policy stops asking, opens the other version
    /// unasked and writes the stored position away. The transport bar already does this, on purpose,
    /// and for the same reason.
    /// </remarks>
    [Fact]
    public async Task A_switch_already_under_way_cannot_be_asked_for_again()
    {
        var inFlight = new TaskCompletionSource();
        var asked = 0;
        var row = new PlayerVersionRowViewModel(
            Version(),
            Question(),
            _ =>
            {
                asked++;
                return inFlight.Task;
            });
        var announcements = 0;
        row.SwitchCommand.CanExecuteChanged += (_, _) => announcements++;

        Assert.True(row.SwitchCommand.CanExecute(null));
        row.SwitchCommand.Execute(null);

        Assert.Equal(1, asked);
        Assert.False(row.SwitchCommand.CanExecute(null));

        // And the button says so, or nothing on screen would grey out.
        Assert.Equal(1, announcements);

        // A press that arrives anyway is refused too: nothing about ICommand promises the caller
        // asked first, and a key binding or code calling this directly reaches the same place.
        row.SwitchCommand.Execute(null);
        Assert.Equal(1, asked);

        inFlight.SetResult();
        for (var attempt = 0; attempt < 100 && !row.SwitchCommand.CanExecute(null); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        // Once the switch is done the row is pressable again, and says that too.
        Assert.True(row.SwitchCommand.CanExecute(null));
        Assert.Equal(2, announcements);
    }

    /// <summary>
    /// The row is pressable again even when the switch it asked for fails. Without the finally that
    /// is a row that can never be pressed again, and the failure would be reported as a dead button.
    /// </summary>
    [Fact]
    public async Task A_switch_that_fails_leaves_the_row_pressable()
    {
        var row = new PlayerVersionRowViewModel(
            Version(),
            Question(),
            _ => Task.FromException(new InvalidOperationException("the version would not open")));

        row.SwitchCommand.Execute(null);
        for (var attempt = 0; attempt < 100 && !row.SwitchCommand.CanExecute(null); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(row.SwitchCommand.CanExecute(null));
        Assert.IsType<InvalidOperationException>(
            Assert.IsType<AsyncRelayCommand>(row.SwitchCommand).LastFailure);
    }

    /// <summary>
    /// A row cannot be pressed while the question its own switch raised is still on screen.
    /// </summary>
    /// <remarks>
    /// The in-flight guard above ends when the switch returns, and a switch that <b>asks</b> returns
    /// straight away: the use case answered <c>Confirm</c>, opened nothing, and the dialogue is
    /// waiting for a person. So the row went back to pressable with the question sitting on top of
    /// it, and a second press started the same switch again from the beginning — which flushes the
    /// playhead, reads the zero a demuxer that has not applied its start position answers, decides
    /// there is nothing worth carrying across, and opens the other version unasked. The question
    /// disappeared with it, because the second switch replaced the decision it was showing, and the
    /// stored position was written away. One press too many cost the progress <b>and</b> the chance
    /// to say what to do about it.
    /// <para>
    /// Measured before the fix, with the question visible: <c>CanExecute</c> answered
    /// <see langword="true"/> and the second press reached the switch. The dialogue is a mandatory
    /// constructor argument rather than an optional one because an optional left at null is the
    /// registered-and-never-fed defect wearing a different hat: it would compile, and the row would
    /// go back to being pressable underneath the question.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_row_cannot_be_pressed_while_its_own_question_is_on_screen()
    {
        var question = Question();
        var asked = 0;
        var row = new PlayerVersionRowViewModel(
            Version(),
            question,
            _ =>
            {
                asked++;
                return Task.CompletedTask;
            });
        var announcements = 0;
        row.SwitchCommand.CanExecuteChanged += (_, _) => announcements++;

        Assert.True(row.SwitchCommand.CanExecute(null));
        question.Apply(ProgressTransferDecision.Confirm(
            TimeSpan.FromMinutes(65),
            ProgressTransferReason.LargeDifference));

        Assert.True(question.IsVisible);
        Assert.False(row.SwitchCommand.CanExecute(null));

        // And the row says so, or the question would sit on top of a row that still looks pressable.
        Assert.Equal(1, announcements);

        // A press that arrives anyway is refused too, which is the press that was measured: the
        // harness pressed again after the first one seemed to do nothing.
        row.SwitchCommand.Execute(null);
        Assert.Equal(0, asked);

        // Answering hands the row back, and says that too. Refusing the question does not rebuild
        // the surfaces — the walk asserts the very same dialogue is still there afterwards — so it
        // has to be this row that becomes pressable again, not a replacement.
        question.CancelCommand.Execute(null);

        Assert.False(question.IsVisible);
        Assert.True(row.SwitchCommand.CanExecute(null));
        Assert.Equal(2, announcements);
    }

    [Fact]
    public void A_surface_with_no_list_at_all_is_refused_rather_than_left_empty()
    {
        Assert.Throws<ArgumentNullException>(() => new PlayerVersionsViewModel(null!));
    }

    private static MediaVersion Version() => new(
        new MediaFileId(Guid.NewGuid()),
        @"R:\media\film-4k.mkv",
        IsAvailable: true,
        TimeSpan.FromMinutes(100),
        3840,
        2160,
        IsHdr: true,
        "HEVC",
        4_000_000_000);

    private static VersionSwitchViewModel Question() => new();
}
