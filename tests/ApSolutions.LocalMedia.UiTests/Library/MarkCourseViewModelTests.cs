// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The add dialog's course half (CRS-001, ADR-0006 amendment 1).
/// </summary>
/// <remarks>
/// A refusal has to arrive as a sentence on the screen, for the reason the root half already
/// learned by hand: letting one escape the command reaches the dispatcher, and on Windows that ends
/// the process.
/// <para>
/// Plain <c>[Fact]</c> and not <c>[AvaloniaFact]</c>, because nothing here reads a resource: the
/// model answers in keys and the surface resolves them. That is the same division that keeps
/// <c>CourseText.Resource</c> — which does read <c>Application.ActualThemeVariant</c> — out of the
/// models.
/// </para>
/// </remarks>
public sealed class MarkCourseViewModelTests
{
    [Fact]
    public void The_dialog_opens_on_its_root_half_and_says_so_in_three_places()
    {
        var viewModel = new MarkCourseViewModel();

        Assert.False(viewModel.IsCourse);
        Assert.True(viewModel.IsRoot);
        Assert.Equal("●", viewModel.RootStateCue);
        Assert.Equal("○", viewModel.CourseStateCue);
        Assert.Equal("AddRootDialogTitle", viewModel.TitleKey);
        Assert.Equal("RootOnboardingDescription", viewModel.HelpKey);
        Assert.Equal("RootAddAction", viewModel.ConfirmKey);
    }

    /// <summary>
    /// The choice travels as a word. AXAML hands a <c>CommandParameter</c> of "True" back as a
    /// string, so a pill written against a boolean would select nothing and look right doing it.
    /// </summary>
    [Fact]
    public void Choosing_the_course_pill_changes_the_title_the_help_and_the_action()
    {
        var viewModel = new MarkCourseViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.SelectKindCommand.Execute("course");

        Assert.True(viewModel.IsCourse);
        Assert.False(viewModel.IsRoot);
        Assert.Equal("○", viewModel.RootStateCue);
        Assert.Equal("●", viewModel.CourseStateCue);
        Assert.Equal("AddCourseTitle", viewModel.TitleKey);
        Assert.Equal("AddCourseHelp", viewModel.HelpKey);
        Assert.Equal("AddCourseConfirmAction", viewModel.ConfirmKey);

        // The shape is drawn while somebody is still deciding, and not on the root half at all.
        Assert.True(viewModel.ShowsShape);
        Assert.Contains(nameof(MarkCourseViewModel.TitleKey), changed);

        viewModel.SelectKindCommand.Execute("root");
        Assert.False(viewModel.IsCourse);

        // Anything that is not the word is not the course half, which is what an unbound parameter
        // arrives as.
        viewModel.SelectKindCommand.Execute(null);
        Assert.False(viewModel.IsCourse);
    }

    /// <summary>
    /// The gesture, end to end: one folder is pointed at, the depth is derived, that folder is
    /// marked, and the two others at the same depth are counted rather than claimed.
    /// </summary>
    [Fact]
    public async Task Marking_one_folder_counts_the_neighbours_instead_of_claiming_them()
    {
        var world = new CourseWorld(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4",
            @"D:\Cursos\Render\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);

        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.Equal("Composición", viewModel.MarkedTitle);
        Assert.True(viewModel.HasMarked);
        Assert.False(viewModel.HasFailure);
        Assert.Equal(2, viewModel.NeighbourCount);
        Assert.True(viewModel.IsAskingAboutNeighbours);
        Assert.Single(world.Courses.Saved);
    }

    /// <summary>
    /// Nothing beside it means nothing to ask, and a dialog demanding an answer it already has is
    /// worse than one that stays quiet.
    /// </summary>
    [Fact]
    public async Task A_course_with_no_neighbours_asks_nothing()
    {
        var world = new CourseWorld(@"D:\Cursos\Composición\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);

        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.Equal("Composición", viewModel.MarkedTitle);
        Assert.Empty(viewModel.Neighbours);
        Assert.False(viewModel.IsAskingAboutNeighbours);
    }

    [Fact]
    public async Task Saying_yes_marks_the_neighbours_and_the_question_goes_away()
    {
        var world = new CourseWorld(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);
        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        await viewModel.MarkNeighboursAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsAskingAboutNeighbours);
        Assert.Equal(2, world.Courses.Saved.Count);

        // And a second yes with nothing left to say yes to changes nothing.
        await viewModel.MarkNeighboursAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, world.Courses.Saved.Count);
    }

    [Fact]
    public async Task Saying_only_this_one_leaves_exactly_the_course_that_was_asked_for()
    {
        var world = new CourseWorld(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);
        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.True(viewModel.DismissNeighboursCommand.CanExecute(null));
        viewModel.DismissNeighboursCommand.Execute(null);

        Assert.False(viewModel.IsAskingAboutNeighbours);
        Assert.False(viewModel.MarkNeighboursCommand.CanExecute(null));
        Assert.Single(world.Courses.Saved);
    }

    /// <summary>
    /// A folder is real and still not a course: of 1955 files measured in one collection only 595
    /// were video, so a folder of nothing but working material is an ordinary thing to point at.
    /// </summary>
    [Fact]
    public async Task A_folder_with_no_video_deep_enough_is_answered_rather_than_marked()
    {
        var world = new CourseWorld(@"D:\Cursos\Composición\apuntes.pdf");
        var viewModel = new MarkCourseViewModel(world.Declare);

        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.Null(viewModel.MarkedTitle);
        Assert.False(viewModel.HasMarked);
        Assert.Equal("AddCourseNoVideoFound", viewModel.FailureKey);
        Assert.Empty(world.Courses.Saved);
    }

    /// <summary>
    /// The three paths no course can be declared from, each answered in words rather than thrown.
    /// </summary>
    [Fact]
    public async Task A_folder_no_course_can_be_declared_from_is_refused_in_words()
    {
        var world = new CourseWorld(@"D:\Cursos\Composición\01 - Intro.mp4");
        world.Roots.Add(@"D:\Cursos");
        var viewModel = new MarkCourseViewModel(world.Declare);

        await viewModel.MarkAsync(@"D:\Cursos", TestContext.Current.CancellationToken);
        Assert.Equal("AddCourseInvalidFolder", viewModel.FailureKey);
        Assert.True(viewModel.HasFailure);

        await viewModel.MarkAsync(@"E:\Composición", TestContext.Current.CancellationToken);
        Assert.Equal("AddCourseInvalidFolder", viewModel.FailureKey);

        await viewModel.MarkAsync(string.Empty, TestContext.Current.CancellationToken);
        Assert.Equal("AddCourseInvalidFolder", viewModel.FailureKey);
    }

    /// <summary>
    /// A parent that already holds a catalogued root cannot become one: a root inside a root is
    /// refused, and the dialog says which of the two problems it is.
    /// </summary>
    [Fact]
    public async Task A_parent_that_would_nest_a_root_is_refused_in_its_own_words()
    {
        var world = new CourseWorld(@"D:\Cursos\Composición\01 - Intro.mp4");
        world.Roots.Add(@"D:\Cursos\Modelado");
        var viewModel = new MarkCourseViewModel(world.Declare);

        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.Equal("RootAddNested", viewModel.FailureKey);
        Assert.Null(viewModel.MarkedTitle);
        Assert.Empty(viewModel.Neighbours);
    }

    /// <summary>
    /// The dialog's one action does whichever half is chosen, because two accented buttons is a
    /// screen with two leading actions.
    /// </summary>
    [Fact]
    public async Task The_one_action_adds_a_root_or_marks_a_course_depending_on_the_pill()
    {
        var world = new CourseWorld(@"D:\Cursos\Composición\01 - Intro.mp4");
        var added = 0;
        var viewModel = new MarkCourseViewModel(world.Declare)
        {
            AddRoot = () =>
            {
                added++;
                return Task.CompletedTask;
            },
        };

        Assert.True(viewModel.ConfirmCommand.CanExecute(null));
        viewModel.ConfirmCommand.Execute(@"D:\Cursos\Composición");
        await SettleAsync(viewModel);
        Assert.Equal(1, added);
        Assert.Empty(world.Courses.Saved);

        // The same one button, on the other half, through the command a person actually presses.
        viewModel.SelectKindCommand.Execute("course");
        viewModel.ConfirmCommand.Execute(@"D:\Cursos\Composición");
        await SettleAsync(viewModel);
        Assert.Single(world.Courses.Saved);
        Assert.Equal(1, added);

        // A press with nothing bound to its parameter is a path of nothing, answered in words.
        viewModel.ConfirmCommand.Execute(null);
        await SettleAsync(viewModel);
        Assert.Equal("AddCourseInvalidFolder", viewModel.FailureKey);
    }

    /// <summary>
    /// «Marcar todas» through its command, which is what the button is wired to — the method
    /// beneath it is already exercised, and a command nobody executes is a line nobody runs.
    /// </summary>
    [Fact]
    public async Task The_neighbours_are_marked_through_the_command_the_button_presses()
    {
        var world = new CourseWorld(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);
        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        viewModel.MarkNeighboursCommand.Execute(null);
        await SettleAsync(viewModel);

        Assert.Equal(2, world.Courses.Saved.Count);
        Assert.False(viewModel.IsAskingAboutNeighbours);
    }

    /// <summary>
    /// A command hands back no task to await, so the settling is what waits for the pass it started.
    /// <see cref="MarkCourseViewModel.IsWorking"/> is set before the first await and cleared in a
    /// finally, which is what makes it the thing to watch.
    /// </summary>
    private static async Task SettleAsync(MarkCourseViewModel viewModel)
    {
        for (var attempt = 0; attempt < 400 && viewModel.IsWorking; attempt++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.False(viewModel.IsWorking, "a pass started by a command never finished.");
    }

    /// <summary>
    /// A composition with no course store, and one whose root half has no delegate: the dialog is
    /// inert rather than broken, which is what a preview and an unassembled test both are.
    /// </summary>
    [Fact]
    public async Task Without_a_use_case_or_a_delegate_nothing_happens_and_nothing_throws()
    {
        var viewModel = new MarkCourseViewModel();

        viewModel.ConfirmCommand.Execute(@"D:\Cursos\Composición");
        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);
        await viewModel.MarkNeighboursAsync(TestContext.Current.CancellationToken);

        Assert.Null(viewModel.MarkedTitle);
        Assert.False(viewModel.HasFailure);
        Assert.False(viewModel.IsWorking);
    }

    /// <summary>
    /// The dialog opens clean, or it opens on the last course's notice and on a question about the
    /// neighbours of a folder nobody is looking at any more.
    /// </summary>
    [Fact]
    public async Task Opening_the_dialog_again_starts_from_nothing()
    {
        var world = new CourseWorld(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);
        viewModel.SelectKindCommand.Execute("course");
        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        viewModel.Begin();

        Assert.False(viewModel.IsCourse);
        Assert.False(viewModel.ShowsShape);
        Assert.Null(viewModel.MarkedTitle);
        Assert.False(viewModel.HasFailure);
        Assert.False(viewModel.IsAskingAboutNeighbours);
    }

    /// <summary>
    /// A button bound to a command asks <c>CanExecute</c> once and then waits to be told.
    /// </summary>
    /// <remarks>
    /// This is the defect the autonomous walk found and no unit test had: «Marcar todas» is
    /// created while there are no neighbours, answers false, and without a raised event stays
    /// disabled for the whole life of the dialog — on screen, correct-looking, and unpressable. What
    /// is asserted is the <b>event</b>, because reading <c>CanExecute</c> straight off the model
    /// gives the right answer whether or not anybody was told.
    /// </remarks>
    [Fact]
    public async Task The_neighbours_buttons_are_told_to_ask_again_when_there_is_something_to_ask()
    {
        var world = new CourseWorld(
            @"D:\Cursos\Composición\01 - Intro.mp4",
            @"D:\Cursos\Modelado\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare);
        var told = 0;
        viewModel.MarkNeighboursCommand.CanExecuteChanged += (_, _) => told++;

        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.True(viewModel.MarkNeighboursCommand.CanExecute(null));
        Assert.True(told > 0, "nothing told the neighbours' button that it had become pressable.");

        told = 0;
        viewModel.DismissNeighboursCommand.Execute(null);
        Assert.False(viewModel.MarkNeighboursCommand.CanExecute(null));
        Assert.True(told > 0, "nothing told the neighbours' button that it had stopped being pressable.");
    }

    /// <summary>
    /// The kind is detected once by the root half and handed here, because a root added from this
    /// side sits on the same volume the path named.
    /// </summary>
    [Fact]
    public async Task A_root_added_from_the_course_half_carries_the_detected_kind()
    {
        var world = new CourseWorld(@"D:\Cursos\Composición\01 - Intro.mp4");
        var viewModel = new MarkCourseViewModel(world.Declare) { Kind = RootKind.Unc };

        await viewModel.MarkAsync(@"D:\Cursos\Composición", TestContext.Current.CancellationToken);

        Assert.Equal(RootKind.Unc, Assert.Single(world.Roots.All).Kind);
    }
}
