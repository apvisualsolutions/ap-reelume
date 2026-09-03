// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Metadata;

/// <summary>
/// Choosing your own cover (LIB-018): the door that did not exist.
/// </summary>
/// <remarks>
/// The store has known how to import a personal image since the artwork work landed, the backup has
/// carried it, and the picker has had a property to hold the answer. Nothing wrote it — measured
/// across the whole tree on 2026-09-03, where the only callers of the import were two tests. These
/// assert the two halves that were missing: that pressing writes it, and that a picker with nothing
/// behind it says so rather than swallowing the press.
/// </remarks>
public sealed class ChooseCoverTests
{
    private static readonly TitleId SomeTitle = new(Guid.NewGuid());

    /// <summary>
    /// A picker built with nothing behind it refuses the press rather than accepting it.
    /// </summary>
    /// <remarks>
    /// This is the failure mode the whole feature is built against: a button that accepts a press
    /// and does nothing is indistinguishable, to the person pressing it, from a broken application.
    /// Several surfaces build a picker only to display it, so this state is real rather than
    /// hypothetical.
    /// </remarks>
    [Fact]
    public void A_picker_with_nothing_behind_it_refuses_the_press()
    {
        var picker = new ArtworkPickerViewModel { Target = SomeTitle };

        Assert.False(picker.CanChoose);
        Assert.False(picker.ChooseCoverCommand.CanExecute(null));
    }

    /// <summary>Nor does it accept one before it knows which title the cover is for.</summary>
    [Fact]
    public void A_picker_with_no_title_refuses_the_press()
    {
        var picker = new ArtworkPickerViewModel(
            _ => Task.FromResult<string?>(@"C:\arte\portada.png"),
            (_, _, _, _) => Task.FromResult(new PersonalCoverResult(CoverImageVerdict.Approved, @"C:\datos\portada.png")));

        Assert.False(picker.CanChoose);
    }

    [Fact]
    public async Task Choosing_an_image_writes_the_path_the_import_answered()
    {
        var picker = Wired(
            chosen: @"C:\arte\portada.png",
            result: new PersonalCoverResult(CoverImageVerdict.Approved, @"C:\datos\personal-artwork\abc\1.png"));

        await Press(picker);

        Assert.Equal(@"C:\datos\personal-artwork\abc\1.png", picker.SelectedPersonalPath);
        Assert.False(string.IsNullOrWhiteSpace(picker.Status));
    }

    /// <summary>
    /// The alternative text is filled from the file's own name when nobody has written one.
    /// </summary>
    /// <remarks>
    /// The store refuses artwork with no alternative text, so without this the first cover anybody
    /// chose would be refused over a field further down the form that they had not reached yet —
    /// a dead end produced by two correct rules meeting.
    /// </remarks>
    [Fact]
    public async Task The_file_name_stands_in_for_an_alternative_text_nobody_wrote()
    {
        string? describedAs = null;
        var picker = new ArtworkPickerViewModel(
            _ => Task.FromResult<string?>(@"C:\arte\Un cartel bonito.png"),
            (_, _, text, _) =>
            {
                describedAs = text;
                return Task.FromResult(new PersonalCoverResult(CoverImageVerdict.Approved, @"C:\datos\1.png"));
            })
        {
            Target = SomeTitle,
        };

        await Press(picker);

        Assert.Equal("Un cartel bonito", describedAs);
        Assert.Equal("Un cartel bonito", picker.AlternativeText);
    }

    /// <summary>One somebody did write is kept rather than overwritten by the file name.</summary>
    [Fact]
    public async Task An_alternative_text_somebody_wrote_is_kept()
    {
        string? describedAs = null;
        var picker = new ArtworkPickerViewModel(
            _ => Task.FromResult<string?>(@"C:\arte\IMG_4471.png"),
            (_, _, text, _) =>
            {
                describedAs = text;
                return Task.FromResult(new PersonalCoverResult(CoverImageVerdict.Approved, @"C:\datos\1.png"));
            })
        {
            Target = SomeTitle,
            AlternativeText = "El cartel de la película",
        };

        await Press(picker);

        Assert.Equal("El cartel de la película", describedAs);
    }

    /// <summary>Every refusal says something, and the path is left alone.</summary>
    /// <remarks>
    /// The three refusals are asserted together because what matters is the property they share: a
    /// cover that did not change with nothing said about it is the worst outcome this can produce,
    /// and it is the one a check written against only the happy path would never see.
    /// </remarks>
    [Theory]
    [InlineData(CoverImageVerdict.NotAnApprovedImage)]
    [InlineData(CoverImageVerdict.TooLarge)]
    [InlineData(CoverImageVerdict.Empty)]
    public async Task A_refusal_is_said_out_loud_and_changes_nothing(CoverImageVerdict verdict)
    {
        var picker = Wired(@"C:\arte\algo.png", new PersonalCoverResult(verdict, null));

        await Press(picker);

        Assert.True(picker.HasStatus, $"{verdict} was refused with nothing said about it.");
        Assert.Null(picker.SelectedPersonalPath);
    }

    /// <summary>
    /// Cancelling the dialog says nothing at all, which is different from being refused.
    /// </summary>
    /// <remarks>
    /// Somebody who changed their mind does not need to be told they did, and a message here would
    /// train people to ignore the line that carries the refusals.
    /// </remarks>
    [Fact]
    public async Task Cancelling_the_dialog_says_nothing()
    {
        var picker = Wired(chosen: null, result: new PersonalCoverResult(CoverImageVerdict.Approved, @"C:\x.png"));

        await Press(picker);

        Assert.False(picker.HasStatus);
        Assert.Null(picker.SelectedPersonalPath);
    }

    /// <summary>
    /// The application as it is actually composed hands the picker both halves it needs.
    /// </summary>
    /// <remarks>
    /// <b>This is the assertion the whole batch exists because nobody had.</b> Everything else here
    /// passes against a picker a test wired itself; what went wrong for months was that the real
    /// composition wired nothing, and no test could tell — a store with no caller and a property
    /// with no writer look exactly like a feature that works, from inside a unit test.
    /// <para>
    /// Read from the composition's source rather than from a built container, which is the accepted
    /// style here for wires no service descriptor can express: what has to be true is that the
    /// registration passes a chooser and an import, and a descriptor only says the type is
    /// registered.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_real_composition_hands_the_picker_both_halves()
    {
        var composition = CompositionSourceText.Read();

        Assert.Contains("new ArtworkPickerViewModel(", composition);
        Assert.Contains("ChooseCoverFileAsync", composition);
        Assert.Contains("SetPersonalCover", composition);

        // And the dialog's filter comes from the allow-list rather than from a second list beside
        // it: offering a kind the import then refuses is the one drift that would show up as the
        // application saying no to a file it had just offered.
        Assert.Contains("CoverImageRules.ApprovedExtensions", composition);
    }

    /// <summary>
    /// Pressing a picker that was built with nothing behind it does nothing and leaves no trace.
    /// </summary>
    /// <remarks>
    /// The command refuses such a press, but <c>ICommand.Execute</c> can be called regardless — a
    /// keyboard, a test, a control that ignores <c>CanExecute</c> — so the guard inside is what
    /// actually holds. Asserted by pressing rather than by asking, because asking is the half
    /// already covered above.
    /// </remarks>
    [Fact]
    public async Task Pressing_an_unwired_picker_does_nothing_at_all()
    {
        var picker = new ArtworkPickerViewModel { Target = SomeTitle };

        await Press(picker);

        Assert.Null(picker.SelectedPersonalPath);
        Assert.False(picker.HasStatus);
    }

    /// <summary>
    /// An import that answers «nothing chosen» is said out loud like any other refusal.
    /// </summary>
    /// <remarks>
    /// It is not the same as cancelling: the dialog handed back a path and the import decided there
    /// was nothing usable in it. Cancelling stays silent; this does not, because from where the
    /// person is standing they DID choose something.
    /// </remarks>
    [Fact]
    public async Task An_import_that_answers_nothing_chosen_still_says_so()
    {
        var picker = Wired(@"C:\arte\algo.png", new PersonalCoverResult(CoverImageVerdict.NothingChosen, null));

        await Press(picker);

        Assert.True(picker.HasStatus);
        Assert.Null(picker.SelectedPersonalPath);
    }

    /// <summary>
    /// Half-wired is not wired: a chooser with no import, and an import with no chooser, both refuse.
    /// </summary>
    /// <remarks>
    /// Two separate conditions guard this and a test that only ever passed both or neither would
    /// leave either one free to be deleted. They are the two halves the composition has to supply,
    /// so they are the two halves asserted apart.
    /// </remarks>
    [Fact]
    public void A_picker_missing_either_half_refuses()
    {
        var noImport = new ArtworkPickerViewModel(_ => Task.FromResult<string?>("x.png")) { Target = SomeTitle };
        Assert.False(noImport.CanChoose);

        var noChooser = new ArtworkPickerViewModel(
            import: (_, _, _, _) => Task.FromResult(new PersonalCoverResult(CoverImageVerdict.Approved, "x.png")))
        {
            Target = SomeTitle,
        };
        Assert.False(noChooser.CanChoose);
    }

    /// <summary>While a choice is in flight the picker refuses a second one.</summary>
    /// <remarks>
    /// Without it a double-press opens two dialogs and the second answer overwrites the first, which
    /// is a cover somebody did not pick. Asserted from inside the dialog itself, because that is the
    /// only moment the state exists.
    /// </remarks>
    [Fact]
    public async Task A_second_press_is_refused_while_the_first_is_in_flight()
    {
        var opened = new TaskCompletionSource();
        var release = new TaskCompletionSource<string?>();
        ArtworkPickerViewModel? picker = null;
        var refusedMidFlight = false;

        picker = new ArtworkPickerViewModel(
            async _ =>
            {
                refusedMidFlight = !picker!.CanChoose;
                opened.SetResult();
                return await release.Task.ConfigureAwait(false);
            },
            (_, _, _, _) => Task.FromResult(new PersonalCoverResult(CoverImageVerdict.Approved, "x.png")))
        {
            Target = SomeTitle,
        };

        Assert.True(picker.CanChoose);
        picker.ChooseCoverCommand.Execute(null);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(refusedMidFlight, "a second press was allowed while the first dialog was open.");

        release.SetResult(null);
        for (var i = 0; i < 200 && picker.IsChoosing; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.True(picker.CanChoose, "the picker never came back after the dialog closed.");
    }

    /// <summary>
    /// Setting a property to what it already holds raises nothing, and the button announces itself
    /// when it becomes pressable.
    /// </summary>
    /// <remarks>
    /// Both halves of one arrangement. The setters short-circuit an unchanged value so a surface is
    /// not redrawn for nothing; the command has to say when the answer to «can this be pressed»
    /// moved, because a target arriving after the surface was drawn would otherwise leave the button
    /// disabled with nothing to wake it.
    /// </remarks>
    [Fact]
    public void An_unchanged_value_is_silent_and_a_pressable_button_announces_itself()
    {
        var picker = new ArtworkPickerViewModel(
            _ => Task.FromResult<string?>("x.png"),
            (_, _, _, _) => Task.FromResult(new PersonalCoverResult(CoverImageVerdict.Approved, "y.png")));

        var announced = 0;
        picker.ChooseCoverCommand.CanExecuteChanged += (_, _) => announced++;

        var changes = 0;
        picker.PropertyChanged += (_, _) => changes++;

        picker.SelectedPersonalPath = "same.png";
        picker.SelectedRemoteUri = new Uri("https://example.invalid/a.png");
        var afterFirst = changes;

        // The same values again: nothing moved, so nothing is said.
        picker.SelectedPersonalPath = "same.png";
        picker.SelectedRemoteUri = new Uri("https://example.invalid/a.png");
        Assert.Equal(afterFirst, changes);

        Assert.False(picker.CanChoose);
        picker.Target = SomeTitle;
        Assert.True(picker.CanChoose);
        Assert.True(announced > 0, "the button never said it had become pressable.");
    }

    private static ArtworkPickerViewModel Wired(string? chosen, PersonalCoverResult result) =>
        new(
            _ => Task.FromResult(chosen),
            (_, _, _, _) => Task.FromResult(result))
        {
            Target = SomeTitle,
        };

    /// <summary>
    /// Presses the command and waits for what it started.
    /// </summary>
    /// <remarks>
    /// <c>ICommand.Execute</c> returns void, so the work it kicks off is not awaitable from here.
    /// <see cref="ArtworkPickerViewModel.IsChoosing"/> is what says whether it is still running, and
    /// polling that is honest about the shape rather than sleeping for a guessed interval — which is
    /// how a test starts passing for the wrong reason on a slower machine.
    /// </remarks>
    private static async Task Press(ArtworkPickerViewModel picker)
    {
        picker.ChooseCoverCommand.Execute(null);

        for (var i = 0; i < 200 && picker.IsChoosing; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.False(picker.IsChoosing, "the picker never finished choosing.");
    }
}
