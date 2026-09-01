// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Movie;
using ApSolutions.LocalMedia.Presentation.Player;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The player's «Lecciones» panel (CRS-004): the course beside the picture, the row being played
/// marked, and every other one a press away.
/// </summary>
/// <remarks>
/// Not one assertion pins a translated literal, for the reason the course card's tests already
/// carry: <c>CourseText</c> falls back to English when the running application has no dictionary for
/// a key, so what is asserted is what does not move — the numbers, the separators, whether two
/// states differ, and whether a line is empty. Anything that reads text is an
/// <c>AvaloniaFact</c>, because the resource lookup reads <c>Application.ActualThemeVariant</c>.
/// </remarks>
public sealed class LessonsPanelViewModelTests
{
    [AvaloniaFact]
    public void The_panel_stacks_the_course_by_module_in_watching_order()
    {
        var panel = new LessonsPanelViewModel(Session(1));

        Assert.Equal(2, panel.Modules.Count);
        Assert.Equal(["Intro", "El nodo"], panel.Modules[0].Lessons.Select(row => row.Title));
        Assert.Equal(["Máscaras"], panel.Modules[1].Lessons.Select(row => row.Title));
    }

    /// <summary>
    /// The row being played, said with the fill, the border and the glyph — three signals, because
    /// both high contrast dictionaries flatten the first two into the resting surface.
    /// </summary>
    [AvaloniaFact]
    public void The_lesson_being_played_is_the_one_marked_current()
    {
        var panel = new LessonsPanelViewModel(Session(1));
        var rows = panel.Modules.SelectMany(module => module.Lessons).ToArray();

        Assert.Equal([false, true, false], rows.Select(row => row.IsCurrent));
    }

    /// <summary>
    /// The lesson being played reads as started even before a single second of progress has been
    /// written for it. Drawn from status alone it would be «○» — the row somebody is watching drawn
    /// as never started, which is the prototype's own <c>partial || curNow</c>.
    /// </summary>
    [AvaloniaFact]
    public void The_row_being_played_never_draws_as_not_started()
    {
        var panel = new LessonsPanelViewModel(Session(1));
        var rows = panel.Modules.SelectMany(module => module.Lessons).ToArray();

        Assert.Equal("○", rows[0].Glyph);
        Assert.Equal("◐", rows[1].Glyph);
        Assert.NotEqual(rows[0].Glyph, rows[1].Glyph);
    }

    [AvaloniaFact]
    public void The_three_states_are_three_different_shapes()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.Watched),
            Lesson(1, 2, "El nodo", status: WatchStatus.InProgress),
            Lesson(1, 3, "Máscaras"),
        };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[0].Id));
        var rows = panel.Modules.SelectMany(module => module.Lessons).ToArray();

        Assert.Equal(3, rows.Select(row => row.Glyph).Distinct().Count());
    }

    /// <summary>
    /// The course's progress and not this lesson's: the transport under the picture already says
    /// where this lesson is, and the column is for how much of the course is left.
    /// </summary>
    [AvaloniaFact]
    public void The_head_counts_watched_lessons_and_what_is_left()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.Watched),
            Lesson(1, 2, "El nodo"),
            Lesson(1, 3, "Máscaras"),
        };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[1].Id));

        Assert.Contains("1/3", panel.Head, StringComparison.Ordinal);
        Assert.Contains(" · ", panel.Head, StringComparison.Ordinal);
        Assert.Contains("20", panel.Head, StringComparison.Ordinal);
    }

    /// <summary>
    /// A finished course says what it did and stops. Hung off the arithmetic instead — «is the
    /// remainder zero» — a course whose last lesson has no duration in the catalogue would say
    /// «0 min restantes», which reads as finished when it is not.
    /// </summary>
    [AvaloniaFact]
    public void A_finished_course_says_the_count_and_nothing_about_time_left()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.Watched),
            Lesson(1, 2, "El nodo", status: WatchStatus.Watched),
        };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[0].Id));

        Assert.Contains("2/2", panel.Head, StringComparison.Ordinal);
        Assert.DoesNotContain(" · ", panel.Head, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lessons loose in the course folder are not a module, and «Módulo 1» drawn over them would
    /// invent a structure the folder does not have.
    /// </summary>
    [AvaloniaFact]
    public void Lessons_loose_in_the_folder_carry_no_module_label()
    {
        var lessons = new[] { Lesson(1, 1, "Intro", module: null) };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[0].Id));

        Assert.False(panel.Modules[0].HasLabel);
        Assert.Empty(panel.Modules[0].Label);
    }

    [AvaloniaFact]
    public void A_module_names_its_number_and_its_title()
    {
        var panel = new LessonsPanelViewModel(Session(1));

        Assert.True(panel.Modules[0].HasLabel);
        Assert.Contains("Fundamentos", panel.Modules[0].Label, StringComparison.Ordinal);
        Assert.Contains("1", panel.Modules[0].Label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Absent rather than «0 min»: a lesson that runs no time is not a thing, and drawing it says
    /// the file is empty rather than that nothing has read it yet.
    /// </summary>
    [AvaloniaFact]
    public void A_lesson_with_no_duration_draws_no_duration_at_all()
    {
        var lessons = new[] { Lesson(1, 1, "Intro", duration: TimeSpan.Zero) };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[0].Id));
        var row = panel.Modules[0].Lessons[0];

        Assert.False(row.HasDuration);
        Assert.Empty(row.Duration);
    }

    [AvaloniaFact]
    public void A_lesson_with_a_duration_names_it_in_minutes()
    {
        var panel = new LessonsPanelViewModel(Session(1));
        var row = panel.Modules[0].Lessons[0];

        Assert.True(row.HasDuration);
        Assert.Contains("10", row.Duration, StringComparison.Ordinal);
    }

    /// <summary>
    /// The glyph is hidden from the automation tree, so the state has to be in the row's name or it
    /// is nowhere: a shape read out loud is the name of a shape.
    /// </summary>
    [AvaloniaFact]
    public void The_row_says_its_state_in_words_and_not_only_as_a_shape()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.Watched),
            Lesson(1, 2, "El nodo"),
        };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[1].Id));
        var rows = panel.Modules[0].Lessons;

        Assert.Contains("Intro", rows[0].AccessibleName, StringComparison.Ordinal);
        Assert.NotEqual(rows[0].AccessibleName, rows[1].AccessibleName);
        Assert.Equal(2, rows.Select(row => row.AccessibleName).Distinct().Count());
    }

    /// <summary>The row being played announces as started, matching the glyph it draws.</summary>
    [AvaloniaFact]
    public void The_row_being_played_announces_the_same_state_its_glyph_draws()
    {
        var lessons = new[] { Lesson(1, 1, "Intro"), Lesson(1, 2, "El nodo") };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[0].Id));
        var rows = panel.Modules[0].Lessons;

        Assert.NotEqual(rows[0].AccessibleName, rows[1].AccessibleName);
    }

    /// <summary>
    /// A lesson left part way through announces its own state, which is neither of the two the
    /// current row and an untouched row already cover: the three have to be three sentences, the
    /// same way the glyphs are three shapes.
    /// </summary>
    [AvaloniaFact]
    public void A_lesson_left_part_way_through_announces_its_own_state()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.InProgress, position: TimeSpan.FromMinutes(4)),
            Lesson(1, 2, "El nodo", status: WatchStatus.Watched),
            Lesson(1, 3, "Máscaras"),
        };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[2].Id));
        var rows = panel.Modules[0].Lessons;

        Assert.Contains("Intro", rows[0].AccessibleName, StringComparison.Ordinal);
        Assert.Equal(3, rows.Select(row => row.AccessibleName).Distinct().Count());
    }

    /// <summary>A lesson with no duration says its name and its state, and nothing where the time went.</summary>
    [AvaloniaFact]
    public void A_row_with_no_duration_still_announces_its_name_and_state()
    {
        var lessons = new[] { Lesson(1, 1, "Intro", duration: TimeSpan.Zero) };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[0].Id));

        Assert.Contains("Intro", panel.Modules[0].Lessons[0].AccessibleName, StringComparison.Ordinal);
    }

    /// <summary>A lesson whose file the catalogue has not seen refuses rather than failing when pressed.</summary>
    [AvaloniaFact]
    public void A_lesson_with_no_file_cannot_be_played()
    {
        var lessons = new[] { Lesson(1, 1, "Intro", hasFile: false), Lesson(1, 2, "El nodo") };
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[1].Id));
        var rows = panel.Modules[0].Lessons;

        Assert.False(rows[0].CanPlay);
        Assert.True(rows[1].CanPlay);
        Assert.False(panel.PlayCommand.CanExecute(rows[0]));
        Assert.True(panel.PlayCommand.CanExecute(rows[1]));
    }

    [AvaloniaFact]
    public void The_command_refuses_anything_that_is_not_a_lesson_row()
    {
        var panel = new LessonsPanelViewModel(Session(1));

        Assert.False(panel.PlayCommand.CanExecute(null));
        Assert.False(panel.PlayCommand.CanExecute("Intro"));
    }

    /// <summary>
    /// Pressing a row asks the shell to open it, with the course as the title and the lesson as the
    /// line under it — which is what the header writes.
    /// </summary>
    [AvaloniaFact]
    public void Pressing_a_row_asks_for_that_lesson_by_name()
    {
        PlayDetailsRequest? asked = null;
        var panel = new LessonsPanelViewModel(Session(1), request =>
        {
            asked = request;
            return Task.CompletedTask;
        });

        panel.PlayCommand.Execute(panel.Modules[1].Lessons[0]);

        Assert.Equal("Máscaras", asked?.Subtitle);
        Assert.Equal("Compositing", asked?.Title);
        Assert.Null(asked?.StartPosition);
    }

    /// <summary>
    /// A lesson left part way through opens where it was left, and one never started opens at the
    /// beginning without asking — the resume offer exists for the case nobody answered.
    /// </summary>
    [AvaloniaFact]
    public void A_lesson_left_part_way_through_is_asked_for_at_its_minute()
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.InProgress, position: TimeSpan.FromMinutes(4)),
            Lesson(1, 2, "El nodo"),
        };
        PlayDetailsRequest? asked = null;
        var panel = new LessonsPanelViewModel(SessionOf(lessons, lessons[1].Id), request =>
        {
            asked = request;
            return Task.CompletedTask;
        });

        panel.PlayCommand.Execute(panel.Modules[0].Lessons[0]);

        Assert.Equal(TimeSpan.FromMinutes(4), asked?.StartPosition);
    }

    /// <summary>Pressed with nobody listening it does nothing, rather than failing.</summary>
    [AvaloniaFact]
    public void A_panel_with_nobody_listening_does_nothing_when_pressed()
    {
        var panel = new LessonsPanelViewModel(Session(1));

        panel.PlayCommand.Execute(panel.Modules[0].Lessons[0]);
    }

    /// <summary>
    /// Re-reading the course moves the counts and the marks without the session changing, which is
    /// what a mark written elsewhere has to be able to do.
    /// </summary>
    [AvaloniaFact]
    public void The_panel_can_be_re_read_around_the_same_lesson()
    {
        var panel = new LessonsPanelViewModel(Session(1));
        var before = panel.Head;
        var lessons = new[]
        {
            Lesson(1, 1, "Intro", status: WatchStatus.Watched),
            Lesson(1, 2, "El nodo"),
            Lesson(2, 3, "Máscaras"),
        };

        panel.Update(SessionOf(lessons, lessons[1].Id));

        Assert.NotEqual(before, panel.Head);
        Assert.Contains("1/3", panel.Head, StringComparison.Ordinal);
        Assert.True(panel.Modules.SelectMany(module => module.Lessons).ElementAt(1).IsCurrent);
    }

    [AvaloniaFact]
    public void The_panel_names_the_course_and_the_lesson_it_is_on()
    {
        var session = Session(1);
        var panel = new LessonsPanelViewModel(session);

        Assert.Equal("Compositing", panel.CourseTitle);
        Assert.Equal(session.LessonId, panel.LessonId);
        Assert.Equal(session.Course.Id, panel.CourseId);
    }

    [AvaloniaFact]
    public void The_note_promises_the_thread_keeps_itself()
    {
        Assert.NotEmpty(LessonsPanelViewModel.Note);
    }

    /// <remarks>
    /// An <c>AvaloniaFact</c> like the rest, and not because it asserts any text. Both arms build a
    /// live panel before they throw, and building one reads <c>Application.ActualThemeVariant</c> —
    /// which, on a thread the framework did not start, answers differently depending on whether some
    /// other test happened to have run first. Measured: green alone, red inside the full suite.
    /// </remarks>
    [AvaloniaFact]
    public void The_session_is_required()
    {
        Assert.Throws<ArgumentNullException>(() => new LessonsPanelViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new LessonsPanelViewModel(Session(1)).Update(null!));
    }

    [AvaloniaFact]
    public void A_row_needs_its_lesson_and_its_command()
    {
        var lesson = Lesson(1, 1, "Intro");

        Assert.Throws<ArgumentNullException>(
            () => new LessonsPanelRowViewModel(null!, false, new StubCommand()));
        Assert.Throws<ArgumentNullException>(() => new LessonsPanelRowViewModel(lesson, false, null!));
    }

    [AvaloniaFact]
    public void A_module_needs_its_lessons()
    {
        Assert.Throws<ArgumentNullException>(
            () => new LessonsPanelModuleViewModel(1, "Fundamentos", null!));
    }

    private static LessonSession Session(int current)
    {
        var lessons = new[]
        {
            Lesson(1, 1, "Intro"),
            Lesson(1, 2, "El nodo"),
            Lesson(2, 3, "Máscaras", module: "Retoque"),
        };

        return SessionOf(lessons, lessons[current].Id);
    }

    private static LessonSession SessionOf(
        IReadOnlyList<CourseLessonProgress> lessons,
        LessonId current)
    {
        var modules = lessons
            .GroupBy(lesson => (lesson.ModuleNumber, lesson.Module))
            .Select(group => new CourseModuleView(group.Key.ModuleNumber, group.Key.Module, [.. group]))
            .ToArray();

        var course = new CourseDetail(
            new CourseId(Guid.NewGuid()),
            "Compositing",
            "Compositing",
            null,
            Domain.Courses.CourseThreadPolicy.Summarise(lessons),
            Domain.Courses.CourseThreadPolicy.Resolve(lessons),
            Domain.Courses.CourseThreadPolicy.Recap(lessons),
            modules,
            modules.Count(module => module.Title is not null),
            lessons.Aggregate(TimeSpan.Zero, (total, lesson) => total + lesson.Duration));

        return new LessonSession(course, current, lessons);
    }

    private static CourseLessonProgress Lesson(
        int moduleNumber,
        int number,
        string title,
        string? module = "Fundamentos",
        bool hasFile = true,
        TimeSpan? duration = null,
        TimeSpan? position = null,
        WatchStatus status = WatchStatus.NotStarted) => new(
        new LessonId(Guid.NewGuid()),
        hasFile ? new MediaFileId(Guid.NewGuid()) : null,
        moduleNumber,
        module,
        number,
        title,
        duration ?? TimeSpan.FromMinutes(10),
        position ?? TimeSpan.Zero,
        status);

    private sealed class StubCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
