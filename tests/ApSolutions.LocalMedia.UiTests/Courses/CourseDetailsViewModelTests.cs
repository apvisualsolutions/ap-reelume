// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Courses;
using ApSolutions.LocalMedia.Presentation.Movie;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Courses;

/// <summary>
/// An opened course (CRS-002, CRS-005): its header, its modules, its thread, and what marking a
/// lesson does to all three.
/// </summary>
/// <remarks>
/// The model is driven through a real <see cref="GetCourses"/> and a real <see cref="SetWatchStatus"/>
/// over in-memory stores rather than through stubs of its own dependencies, and that is deliberate on
/// two counts. The first is coverage arithmetic that cost this repository a round of CI: the
/// constructor sweep hands every Presentation constructor a null and takes the throwing arm, so the
/// non-throwing arm has to be taken <b>in the same suite</b> — the merged Cobertura report keeps the
/// best single report for a line rather than the union of them, so a guard exercised from
/// <c>UiTests</c> on one side and <c>IntegrationTests</c> on the other reads 1/2 for ever.
/// <para>
/// The second is that marking a lesson is not a write and then a claim: it writes through PLY-008's
/// store and re-reads the course, so the thread only moves if the round trip really happened. The
/// lesson reader here derives each lesson's status from the watch store for exactly that reason —
/// a stub that returned a fixed list would let the model claim a thread that never moved.
/// </para>
/// <para>
/// Not one assertion pins a translated literal. <c>CourseText</c> falls back to English when the
/// running application has no dictionary for a key, so what is asserted is what does not move: the
/// numbers, the separators, whether two states differ, and whether a line is empty. Everything that
/// reads <c>Meta</c>, <c>ProgressText</c> or a row's text is an <c>AvaloniaFact</c>, because
/// <c>CourseText.Resource</c> reads <c>Application.ActualThemeVariant</c> and that verifies the UI
/// thread.
/// </para>
/// <para>
/// <b>Three branches of this file are unreachable, and they are written here because a measured
/// ceiling belongs where somebody will look again.</b> The file reads 100 % of lines and 91 of 94
/// branches. The three left are:
/// </para>
/// <list type="bullet">
/// <item><c>Progress</c>'s <c>_detail is { Summary.TotalLessons: &gt; 0 }</c> and <c>ThreadMinute</c>'s
/// <c>_detail is { Thread.IsPartial: true }</c>, one arm each. A property pattern emits a null check
/// for every member it walks through, and <c>CourseDetail.Summary</c> and <c>CourseDetail.Thread</c>
/// are non-nullable positional members of a record — no caller can hand over a detail whose summary
/// or thread is null, so the check can only ever answer once.</item>
/// <item><c>ResumeThreadAsync</c>'s <c>ThreadLessonRow is { } row</c>, by the null arm.
/// <c>AsyncRelayCommand.Execute</c> asks <c>CanExecute</c> first and that is
/// <c>ThreadLessonRow is not null</c>, so the only caller there is refuses precisely the case the
/// guard is for. It stays because the two are wired apart — the command's predicate is fixed in the
/// constructor and the property is rebuilt on every load — and a guard that costs one branch is
/// cheaper than a button with nothing behind it.</item>
/// </list>
public sealed class CourseDetailsViewModelTests
{
    private static readonly MediaFileId SomeFile = new(Guid.NewGuid());

    /// <summary>
    /// A part-watched course, which is the state every header line has a different answer for: a
    /// module with a title beside lessons loose in the folder, a lesson left part way through, and
    /// time still to go.
    /// </summary>
    [AvaloniaFact]
    public async Task A_part_watched_course_names_its_modules_its_progress_and_where_the_thread_is()
    {
        var world = World.WithACourse();

        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        Assert.True(model.HasCourse);
        Assert.Equal("Un curso", model.Title);
        Assert.Equal(@"Cursos\Un curso", model.RelativePath);

        // One module of the two carries a title; the loose lessons are not a module and «Módulo 2»
        // over them would invent one.
        Assert.Equal(2, model.Modules.Count);
        Assert.True(model.Modules[0].HasLabel);
        Assert.NotEqual(string.Empty, model.Modules[0].Label);
        Assert.False(model.Modules[1].HasLabel);
        Assert.Equal(string.Empty, model.Modules[1].Label);
        Assert.Contains("1", model.Modules[0].Count, StringComparison.Ordinal);

        // Header: 1 module counted, 5 lessons, and a duration built from the four that have one.
        Assert.Contains("5", model.Meta, StringComparison.Ordinal);
        Assert.Contains(" · ", model.Meta, StringComparison.Ordinal);

        // 1 of 5 watched, and time left, so the line keeps both halves.
        Assert.Contains("1", model.ProgressText, StringComparison.Ordinal);
        Assert.Contains(" · ", model.ProgressText, StringComparison.Ordinal);
        Assert.Equal(0.2, model.Progress);
        Assert.False(model.IsFinished);

        // The thread points at the lesson left part way through, so it names it and offers a minute.
        Assert.NotEqual(string.Empty, model.ThreadLesson);
        Assert.True(model.HasThreadMinute);
        Assert.NotEqual(string.Empty, model.ThreadMinute);
        Assert.NotEqual(string.Empty, model.ThreadActionText);
        Assert.Equal(world.Second, model.ThreadLessonRow?.Id);
        Assert.True(model.HasRecap);
        Assert.NotEmpty(model.Recap);
    }

    /// <summary>
    /// Nothing is loaded, which is the state the card is built in and drawn in for one frame. Every
    /// line has to be empty rather than absent, and no property may throw reaching for a course that
    /// is not there.
    /// </summary>
    [AvaloniaFact]
    public async Task A_card_with_no_course_draws_empty_lines_rather_than_failing()
    {
        var world = World.WithACourse();
        var model = world.Model();

        Assert.False(model.HasCourse);
        Assert.Equal(string.Empty, model.Title);
        Assert.Equal(string.Empty, model.RelativePath);
        Assert.Equal(string.Empty, model.Meta);
        Assert.Equal(string.Empty, model.ProgressText);
        Assert.Equal(string.Empty, model.ThreadLesson);
        Assert.Equal(string.Empty, model.ThreadMinute);
        Assert.False(model.HasThreadMinute);
        Assert.False(model.HasMarkNotice);
        Assert.Equal(0, model.Progress);
        Assert.False(model.IsFinished);
        Assert.Empty(model.Modules);
        Assert.Empty(model.Recap);
        Assert.Null(model.ThreadLessonRow);
        Assert.False(model.ResumeThreadCommand.CanExecute(null));

        // And a course that is asked for and is not there leaves it in the same state rather than
        // half of the last one.
        await model.LoadAsync(new CourseId(Guid.NewGuid()));

        Assert.False(model.HasCourse);
        Assert.Empty(model.Modules);
        Assert.Null(model.ThreadLessonRow);
    }

    /// <summary>
    /// A finished course, which is the arm every «and what is left» half is dropped on — and it still
    /// keeps a row to play, because «Volver a empezar» with nothing behind it would be a button that
    /// looks pressable and is not.
    /// </summary>
    [AvaloniaFact]
    public async Task A_finished_course_drops_the_time_left_and_still_offers_the_first_lesson()
    {
        var world = World.WithACourse();
        await world.MarkEveryLessonWatched();

        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        Assert.True(model.IsFinished);
        Assert.Equal(1, model.Progress);
        Assert.DoesNotContain(" · ", model.ProgressText, StringComparison.Ordinal);

        // Nothing left to point at, so the thread line goes quiet and the button changes its offer.
        Assert.Equal(string.Empty, model.ThreadLesson);
        Assert.Equal(string.Empty, model.ThreadMinute);
        Assert.False(model.HasThreadMinute);

        // «Lo último que viste» is the last two finished, so a course with everything finished has
        // the fullest recap of all rather than none.
        Assert.True(model.HasRecap);
        Assert.Equal(2, model.Recap.Count);

        // But the button still leads somewhere: the first lesson of the course.
        Assert.Equal(world.First, model.ThreadLessonRow?.Id);
        Assert.True(model.ResumeThreadCommand.CanExecute(null));

        // One label read off one flag, so the button and the finished chip can never disagree: a
        // course with something left offers a different word from one with nothing left.
        var other = World.WithACourse();
        var unfinished = other.Model();
        await unfinished.LoadAsync(other.CourseId);

        Assert.False(unfinished.IsFinished);

        Assert.NotEqual(string.Empty, model.ThreadActionText);
        Assert.NotEqual(unfinished.ThreadActionText, model.ThreadActionText);
    }

    /// <summary>
    /// A folder marked and not yet walked: a course the store knows about and the reader knows
    /// nothing about. Every number divides by none rather than failing, and the thread has nothing
    /// to point at.
    /// </summary>
    [AvaloniaFact]
    public async Task A_marked_folder_that_has_not_been_walked_yet_divides_by_none()
    {
        var world = World.WithAnUnwalkedFolder();

        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        Assert.True(model.HasCourse);
        Assert.Equal(0, model.Progress);
        Assert.False(model.IsFinished);
        Assert.Empty(model.Modules);
        Assert.Empty(model.Recap);
        Assert.Null(model.ThreadLessonRow);
        Assert.False(model.ResumeThreadCommand.CanExecute(null));

        // The header still counts, and what it counts is zero rather than nothing.
        Assert.Contains("0", model.Meta, StringComparison.Ordinal);
        Assert.Contains("0", model.ProgressText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The thread on a course nobody has touched points at the first lesson and offers no minute:
    /// «continuar» is not «reanudar en 4:00», and a course at zero has no minute to name.
    /// </summary>
    [AvaloniaFact]
    public async Task An_untouched_course_points_at_its_first_lesson_without_a_minute()
    {
        var world = World.WithACourse(untouched: true);

        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        Assert.False(model.IsFinished);
        Assert.Equal(0, model.Progress);
        Assert.Equal(world.First, model.ThreadLessonRow?.Id);
        Assert.NotEqual(string.Empty, model.ThreadLesson);
        Assert.Equal(string.Empty, model.ThreadMinute);
        Assert.False(model.HasThreadMinute);

        // Nothing finished, so there is nothing to be reminded of.
        Assert.False(model.HasRecap);
        Assert.Empty(model.Recap);
    }

    /// <summary>
    /// The three states of a lesson row as shapes rather than as colours, and the two it refuses:
    /// a lesson the catalogue has not seen cannot be played, and one with no length draws no bar.
    /// </summary>
    [AvaloniaFact]
    public async Task A_lesson_row_says_which_of_the_three_states_it_is_in()
    {
        var world = World.WithACourse();
        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        var rows = model.Modules.SelectMany(module => module.Lessons).ToArray();
        var watched = rows.Single(row => row.Id == world.First);
        var partial = rows.Single(row => row.Id == world.Second);
        var fresh = rows.Single(row => row.Id == world.Third);
        var lengthless = rows.Single(row => row.Id == world.Lengthless);
        var missing = rows.Single(row => row.Id == world.Missing);

        // Three states, three glyphs, and no two of them the same.
        Assert.Equal(3, new[] { watched.Glyph, partial.Glyph, fresh.Glyph }.Distinct(StringComparer.Ordinal).Count());

        Assert.True(watched.IsWatched);
        Assert.False(partial.IsWatched);
        Assert.False(watched.HasBar);
        Assert.True(partial.HasBar);
        Assert.False(fresh.HasBar);
        Assert.Equal(0.25, partial.Progress);
        Assert.Equal(0, lengthless.Progress);
        Assert.False(lengthless.HasBar);

        // The row the thread points at is the only one marked as such.
        Assert.True(partial.IsNextInThread);
        Assert.Single(rows, row => row.IsNextInThread);

        // Every row is bound to the card's own two commands rather than to a copy of them, which is
        // what lets the card refuse a press without every row having to know why.
        Assert.Same(model.PlayCommand, watched.PlayCommand);
        Assert.Same(model.ToggleWatchedCommand, watched.ToggleWatchedCommand);

        // A lesson whose file the catalogue has not seen refuses rather than failing when pressed.
        Assert.True(watched.CanPlay);
        Assert.False(missing.CanPlay);
        Assert.Null(missing.MediaFileId);

        // Each state's line differs from the others', and the mark button offers the opposite of
        // whatever the row already is.
        Assert.NotEqual(watched.Meta, partial.Meta);
        Assert.NotEqual(partial.Meta, fresh.Meta);
        Assert.Contains(" · ", partial.Meta, StringComparison.Ordinal);
        Assert.DoesNotContain(" · ", lengthless.Meta, StringComparison.Ordinal);
        Assert.NotEqual(watched.MarkActionText, fresh.MarkActionText);

        // What a screen reader gets instead of four labels in a row.
        Assert.Contains(watched.Number, watched.AccessibleName, StringComparison.Ordinal);
        Assert.Contains(watched.Title, watched.AccessibleName, StringComparison.Ordinal);
        Assert.Contains(watched.Number, watched.MarkAccessibleName, StringComparison.Ordinal);
        Assert.Contains(watched.MarkActionText, watched.MarkAccessibleName, StringComparison.Ordinal);
        Assert.StartsWith("L", watched.Number, StringComparison.Ordinal);
    }

    /// <summary>
    /// Playing a lesson asks the shell, because the shell owns the player. The request carries the
    /// minute only when there is one to carry — a lesson at zero starts at the start rather than
    /// asking to be resumed from nowhere.
    /// </summary>
    [AvaloniaFact]
    public async Task Playing_a_lesson_hands_the_shell_the_file_and_the_minute()
    {
        var world = World.WithACourse();
        var model = world.Model();
        var asked = new List<PlayDetailsRequest>();
        model.PlayRequested = request =>
        {
            asked.Add(request);
            return Task.CompletedTask;
        };
        await model.LoadAsync(world.CourseId);

        var rows = model.Modules.SelectMany(module => module.Lessons).ToArray();
        var partial = rows.Single(row => row.Id == world.Second);
        var fresh = rows.Single(row => row.Id == world.Third);
        var missing = rows.Single(row => row.Id == world.Missing);

        // The command refuses anything that is not a playable row, which is why the arm behind it
        // never has to test the parameter again.
        Assert.True(model.PlayCommand.CanExecute(partial));
        Assert.False(model.PlayCommand.CanExecute(missing));
        Assert.False(model.PlayCommand.CanExecute(null));
        Assert.False(model.PlayCommand.CanExecute("not a lesson"));

        model.PlayCommand.Execute(partial);
        model.PlayCommand.Execute(fresh);

        Assert.Equal(2, asked.Count);
        Assert.Equal(SomeFile, asked[0].MediaFileId);
        Assert.Equal(TimeSpan.FromMinutes(6), asked[0].StartPosition);
        Assert.Equal(model.Title, asked[0].Title);
        Assert.Equal(partial.Title, asked[0].Subtitle);

        // The untouched lesson carries no minute rather than a zero one.
        Assert.Null(asked[1].StartPosition);
    }

    /// <summary>
    /// The thread panel's own button plays the row the thread points at, and a card nobody has
    /// wired a player to says nothing rather than failing.
    /// </summary>
    [AvaloniaFact]
    public async Task The_thread_button_plays_the_row_the_thread_points_at()
    {
        var world = World.WithACourse();
        var model = world.Model();
        var asked = new List<PlayDetailsRequest>();
        await model.LoadAsync(world.CourseId);

        // No player wired yet: the button runs and asks nobody, rather than throwing at a card that
        // is drawn before the shell has finished wiring it.
        model.ResumeThreadCommand.Execute(null);
        Assert.Empty(asked);

        model.PlayRequested = request =>
        {
            asked.Add(request);
            return Task.CompletedTask;
        };
        model.ResumeThreadCommand.Execute(null);

        Assert.Single(asked);
        Assert.Equal(model.ThreadLessonRow?.MediaFileId, asked[0].MediaFileId);
    }

    /// <summary>
    /// Marking a lesson moves the thread, and says so in words: somebody reading with a screen reader
    /// gets nothing from a glyph that changed further down the page.
    /// </summary>
    [AvaloniaFact]
    public async Task Marking_a_lesson_moves_the_thread_and_announces_it()
    {
        var world = World.WithACourse();
        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        Assert.False(model.HasMarkNotice);
        Assert.Equal(string.Empty, model.MarkNotice);

        var partial = model.Modules.SelectMany(module => module.Lessons).Single(row => row.Id == world.Second);
        Assert.True(model.ToggleWatchedCommand.CanExecute(partial));
        model.ToggleWatchedCommand.Execute(partial);

        // It went through the watch store, so the re-read moved the thread on to the next lesson.
        Assert.True(model.HasMarkNotice);
        var marked = model.MarkNotice;
        Assert.NotEqual(string.Empty, marked);
        Assert.Equal(world.Third, model.ThreadLessonRow?.Id);
        Assert.Equal(0.4, model.Progress);

        // Handing it back is a different sentence, and it puts the thread where it was.
        var again = model.Modules.SelectMany(module => module.Lessons).Single(row => row.Id == world.Second);
        model.ToggleWatchedCommand.Execute(again);

        Assert.NotEqual(marked, model.MarkNotice);
        Assert.Equal(world.Second, model.ThreadLessonRow?.Id);
        Assert.Equal(0.2, model.Progress);

        // And opening a course again never re-announces last time's mark.
        await model.LoadAsync(world.CourseId);
        Assert.False(model.HasMarkNotice);
        Assert.Equal(string.Empty, model.MarkNotice);
    }

    /// <summary>
    /// The two the mark refuses: a lesson with no file behind it, and a card with no course loaded.
    /// Both return without writing rather than failing when pressed.
    /// </summary>
    [AvaloniaFact]
    public async Task Marking_refuses_a_lesson_with_no_file_and_a_card_with_no_course()
    {
        var world = World.WithACourse();
        var model = world.Model();
        await model.LoadAsync(world.CourseId);

        var missing = model.Modules.SelectMany(module => module.Lessons).Single(row => row.Id == world.Missing);
        Assert.False(model.ToggleWatchedCommand.CanExecute(missing));
        Assert.False(model.ToggleWatchedCommand.CanExecute(null));
        Assert.False(model.ToggleWatchedCommand.CanExecute("not a lesson"));

        // Pressed anyway — which is what a bound surface does the frame before CanExecute is read —
        // it writes nothing and says nothing.
        model.ToggleWatchedCommand.Execute(missing);
        Assert.False(model.HasMarkNotice);
        Assert.Equal(0, world.Writes);

        // And a playable row handed to a card whose course never loaded. This is the one the command
        // itself cannot refuse — the row has a file, so CanExecute says yes — and it is the reason
        // the body asks about the course as well as about the file.
        var playable = model.Modules.SelectMany(module => module.Lessons).Single(row => row.Id == world.First);
        var blank = world.Model();

        Assert.True(blank.ToggleWatchedCommand.CanExecute(playable));

        blank.ToggleWatchedCommand.Execute(playable);

        Assert.False(blank.HasMarkNotice);
        Assert.False(blank.HasCourse);
        Assert.Equal(0, world.Writes);
    }

    /// <summary>
    /// Loading tells whoever is bound, and loading with nobody bound is not a failure: the card is
    /// built before the view attaches to it, and the notification has to survive having no listener.
    /// </summary>
    [AvaloniaFact]
    public async Task Loading_notifies_whoever_is_listening_and_survives_nobody_listening()
    {
        var world = World.WithACourse();

        var unheard = world.Model();
        await unheard.LoadAsync(world.CourseId);
        Assert.True(unheard.HasCourse);

        var model = world.Model();
        var announced = new List<string?>();
        model.PropertyChanged += (_, e) => announced.Add(e.PropertyName);
        await model.LoadAsync(world.CourseId);

        Assert.Contains(nameof(CourseDetailsViewModel.HasCourse), announced, StringComparer.Ordinal);
        Assert.Contains(nameof(CourseDetailsViewModel.Modules), announced, StringComparer.Ordinal);
        Assert.Contains(nameof(CourseDetailsViewModel.ThreadLessonRow), announced, StringComparer.Ordinal);
        Assert.Contains(nameof(CourseDetailsViewModel.MarkNotice), announced, StringComparer.Ordinal);
    }

    /// <summary>
    /// The guards, taken from the other side. The constructor sweep hands every one of these a null
    /// and takes the throwing arm; this takes the arm that carries on, and it has to happen in this
    /// suite or the merged report reads the pair as half covered for ever.
    /// </summary>
    [Fact]
    public void Every_course_model_needs_what_it_was_given_and_keeps_it()
    {
        var world = World.WithACourse();

        Assert.Throws<ArgumentNullException>(() => new CourseDetailsViewModel(null!, world.SetWatchStatus));
        Assert.Throws<ArgumentNullException>(() => new CourseDetailsViewModel(world.GetCourses, null!));
        Assert.NotNull(world.Model());

        var lesson = World.Lesson(Guid.NewGuid(), 1, 1, "Una lección", WatchStatus.NotStarted);
        var command = world.Model().PlayCommand;

        Assert.Throws<ArgumentNullException>(() => new LessonRowViewModel(null!, false, command, command));
        Assert.Throws<ArgumentNullException>(() => new LessonRowViewModel(lesson, false, null!, command));
        Assert.Throws<ArgumentNullException>(() => new LessonRowViewModel(lesson, false, command, null!));
        Assert.NotNull(new LessonRowViewModel(lesson, false, command, command));

        // The module has no title, and that is measured rather than tidy: a titled one builds its
        // label through CourseText.Resource, which reads Application.ActualThemeVariant and so
        // VERIFIES THE UI THREAD. A plain Fact building one passes under --filter, where no
        // application exists, and throws "the calling thread cannot access this object" with the
        // whole suite, where one does. The titled arm is covered by the AvaloniaFacts above.
        var module = new CourseModuleView(1, null, [lesson]);

        Assert.Throws<ArgumentNullException>(() => new CourseModuleViewModel(null!, []));
        Assert.Throws<ArgumentNullException>(() => new CourseModuleViewModel(module, null!));
        Assert.False(new CourseModuleViewModel(module, []).HasLabel);
    }

    /// <summary>
    /// The stores the model is driven over, and the course they hold: two modules, five lessons, and
    /// one of them left part way through.
    /// </summary>
    private sealed class World
    {
        private readonly StubCourses _courses = new();
        private readonly StubLessons _lessons;
        private readonly CountingWatchStates _states = new();

        private World()
        {
            _lessons = new StubLessons(_states);
            GetCourses = new GetCourses(_courses, _lessons);
            SetWatchStatus = new SetWatchStatus(_states, new FixedClock());
        }

        public GetCourses GetCourses { get; }

        public SetWatchStatus SetWatchStatus { get; }

        public CourseId CourseId { get; } = new(Guid.NewGuid());

        public LessonId First { get; } = new(Guid.NewGuid());

        public LessonId Second { get; } = new(Guid.NewGuid());

        public LessonId Third { get; } = new(Guid.NewGuid());

        public LessonId Lengthless { get; } = new(Guid.NewGuid());

        public LessonId Missing { get; } = new(Guid.NewGuid());

        public int Writes => _states.Writes;

        /// <summary>
        /// Five lessons over two modules: one watched, one left part way through, one untouched, one
        /// with no length at all, and one whose file the catalogue has never seen. The second module
        /// has no title, which is what lessons loose in the course folder look like.
        /// </summary>
        public static World WithACourse(bool untouched = false)
        {
            var world = new World();
            world._courses.Courses.Add(new Course(
                world.CourseId,
                new LibraryRootId(Guid.NewGuid()),
                @"Cursos\Un curso",
                "Un curso",
                new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
                null));

            world._lessons.ByCourse[world.CourseId] =
            [
                Lesson(
                    world.First.Value, 1, 1, "La primera",
                    untouched ? WatchStatus.NotStarted : WatchStatus.Watched),
                Lesson(
                    world.Second.Value, 1, 2, "La segunda",
                    untouched ? WatchStatus.NotStarted : WatchStatus.InProgress,
                    position: untouched ? TimeSpan.Zero : TimeSpan.FromMinutes(6),
                    duration: TimeSpan.FromMinutes(24)),
                Lesson(world.Third.Value, 1, 3, "La tercera", WatchStatus.NotStarted),
                Lesson(
                    world.Lengthless.Value, 2, 4, "Sin duración", WatchStatus.NotStarted,
                    module: null, duration: TimeSpan.Zero),
                Lesson(
                    world.Missing.Value, 2, 5, "Sin archivo", WatchStatus.NotStarted,
                    module: null, hasFile: false),
            ];

            return world;
        }

        /// <summary>
        /// A folder somebody marked whose walk has not run yet: the course row exists and the reader
        /// knows nothing about it, which the grid draws rather than hides.
        /// </summary>
        public static World WithAnUnwalkedFolder()
        {
            var world = new World();
            world._courses.Courses.Add(new Course(
                world.CourseId,
                new LibraryRootId(Guid.NewGuid()),
                @"Cursos\Recién marcado",
                "Recién marcado",
                new DateTimeOffset(2026, 8, 30, 9, 0, 0, TimeSpan.Zero),
                null));

            return world;
        }

        public static CourseLessonProgress Lesson(
            Guid id,
            int moduleNumber,
            int number,
            string title,
            WatchStatus status,
            TimeSpan? position = null,
            TimeSpan? duration = null,
            string? module = "Introducción",
            bool hasFile = true) => new(
                new LessonId(id),
                hasFile ? SomeFile : null,
                moduleNumber,
                module,
                number,
                title,
                duration ?? TimeSpan.FromMinutes(10),
                position ?? TimeSpan.Zero,
                status);

        public CourseDetailsViewModel Model() => new(GetCourses, SetWatchStatus);

        /// <summary>Marks every lesson that has a file, which is what finishes the course.</summary>
        public async Task MarkEveryLessonWatched()
        {
            foreach (var lesson in _lessons.ByCourse[CourseId])
            {
                await SetWatchStatus.MarkAsync(
                    CourseProgressKey.For(CourseId, lesson.Id),
                    lesson.MediaFileId ?? SomeFile,
                    WatchStatus.Watched);
            }
        }
    }

    private sealed class StubCourses : ICourseRepository
    {
        public List<Course> Courses { get; } = [];

        public Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Course>>(Courses);

        public Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Courses.FirstOrDefault(course => course.Id == id));

        public Task<CourseId> SaveAsync(
            Course course,
            IReadOnlyList<Lesson> lessons,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Lesson>> ListLessonsAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Lesson?> FindLessonByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(
            CourseId id,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    /// <summary>
    /// The lessons, with each one's status read back out of the watch store rather than held here.
    /// That is what makes marking a lesson observable: the model writes through PLY-008 and re-reads
    /// the course, so a thread that moves proves the round trip happened.
    /// </summary>
    private sealed class StubLessons(CountingWatchStates states) : ICourseLessonReader
    {
        public Dictionary<CourseId, IReadOnlyList<CourseLessonProgress>> ByCourse { get; } = [];

        public Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Current(courseId));

        public Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>>(
                ByCourse.Keys.ToDictionary(course => course, Current));

        private IReadOnlyList<CourseLessonProgress> Current(CourseId courseId) =>
            ByCourse.TryGetValue(courseId, out var lessons)
                ? [.. lessons.Select(lesson =>
                    states.Find(CourseProgressKey.For(courseId, lesson.Id)) is { } stored
                        ? lesson with { Status = stored.Status }
                        : lesson)]
                : [];
    }

    private sealed class CountingWatchStates : IWatchStateRepository
    {
        private readonly Dictionary<string, WatchState> _stored = [];

        public int Writes { get; private set; }

        public WatchState? Find(ContentKey content) =>
            _stored.TryGetValue(content.Value, out var state) ? state : null;

        public Task<WatchState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default) =>
            Task.FromResult(Find(content));

        public Task<IReadOnlyList<WatchState>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WatchState>>([.. _stored.Values]);

        public Task SaveAsync(WatchState state, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            Writes++;
            _stored[state.Content.Value] = state;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
