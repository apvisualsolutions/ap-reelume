// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Courses;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Courses;

/// <summary>
/// The courses grid (CRS-003): what it lists, the two separate offers a card makes, and the positive
/// empty state that offers to mark a folder.
/// </summary>
/// <remarks>
/// The model is built over a real <see cref="GetCourses"/> rather than a stub of it, and that is
/// coverage arithmetic rather than taste: the constructor sweep hands every Presentation constructor
/// a null and takes the throwing arm, so the arm that carries on has to be taken <b>in this same
/// suite</b>. The merged Cobertura report keeps the best single report for a line instead of the
/// union of them, so a guard thrown at from here and satisfied somewhere else reads 1/2 for ever.
/// <para>
/// Nothing here pins a translated literal, with one deliberate exception noted where it happens: the
/// test that puts a value into the running application's dictionary and reads it back is asserting
/// that <i>its own</i> value came out, not that a shipped string says anything in particular.
/// </para>
/// <para>
/// <b>One branch of this file is unreachable and it is measured rather than assumed.</b> The file
/// reads 100 % of lines and 35 of 36 branches; the one left is
/// <c>Application.Current is { }</c> in <c>CourseText.Resource</c>, taken by the arm where there is
/// <i>no</i> application. Every test that reads a word needs a running application — the lookup goes
/// through <c>ActualThemeVariant</c>, which verifies the UI thread — and an application, once the
/// headless framework has built one for the assembly, is a process-wide static that no test can take
/// away. It was not deduced: a plain <c>Fact</c> here reached
/// <c>Application.get_ActualThemeVariant</c> and threw "the calling thread cannot access this
/// object", which is proof that <c>Current</c> was not null. The other five of its six branches are
/// covered, by the test below that supplies the key, removes it, and puts a non-string under it.
/// </para>
/// </remarks>
public sealed class CoursesViewModelTests
{
    /// <summary>
    /// The grid lists what the use case hands it, and each card carries the identity and the folder
    /// the row draws.
    /// </summary>
    [AvaloniaFact]
    public async Task The_grid_lists_the_courses_the_use_case_hands_it()
    {
        var world = new World();
        var partWatched = world.AddCourse("Un curso", @"Cursos\Un curso", watched: 1, total: 4);

        var model = new CoursesViewModel(world.GetCourses);

        // Before it is loaded there is nothing, which is the state the destination is built in.
        Assert.True(model.IsEmpty);
        Assert.False(model.HasCourses);
        Assert.Empty(model.Cards);

        await model.LoadAsync();

        var card = Assert.Single(model.Cards);
        Assert.False(model.IsEmpty);
        Assert.True(model.HasCourses);
        Assert.Equal(partWatched, card.Id);
        Assert.Equal("Un curso", card.Title);
        Assert.Equal(@"Cursos\Un curso", card.RelativePath);
        Assert.Equal(0.25, card.Progress);
        Assert.True(card.CanAct);
        Assert.False(card.IsFinished);
        Assert.NotNull(card.OpenCommand);
        Assert.NotNull(card.ResumeCommand);
    }

    /// <summary>
    /// The empty state is what somebody sees first and it is not a failure, so the destination says
    /// so and hands over the command that resolves it — which the shell owns, because the door to the
    /// add-media dialog already belongs to the shell.
    /// </summary>
    [AvaloniaFact]
    public async Task An_empty_grid_says_so_and_carries_the_shell_s_own_way_out()
    {
        var world = new World();
        var model = new CoursesViewModel(world.GetCourses);

        await model.LoadAsync();

        Assert.True(model.IsEmpty);
        Assert.False(model.HasCourses);
        Assert.Empty(model.Cards);

        // Nothing wired it yet, which is how it is drawn for the first frame.
        Assert.Null(model.MarkFolderCommand);

        var wired = new CountingCommand();
        model.MarkFolderCommand = wired;

        Assert.Same(wired, model.MarkFolderCommand);
    }

    /// <summary>
    /// Opening and resuming are different offers — one shows the lessons, the other starts playing —
    /// so they are separate events, and each carries the course rather than the card.
    /// </summary>
    [AvaloniaFact]
    public async Task Opening_and_resuming_are_two_offers_and_not_one()
    {
        var world = new World();
        var course = world.AddCourse("Un curso", @"Cursos\Un curso", watched: 1, total: 4);
        var model = new CoursesViewModel(world.GetCourses);
        var opened = new List<CourseId>();
        var resumed = new List<CourseId>();
        model.Opened += (_, id) => opened.Add(id);
        model.Resumed += (_, id) => resumed.Add(id);
        await model.LoadAsync();

        var card = Assert.Single(model.Cards);

        card.OpenCommand.Execute(card);
        card.ResumeCommand.Execute(card);

        Assert.Equal([course], opened);
        Assert.Equal([course], resumed);
    }

    /// <summary>
    /// What the two commands refuse. Resuming asks for more than opening does: a folder still being
    /// walked can be opened and has nothing to carry on with, which is the whole reason the card
    /// carries <see cref="CourseCardViewModel.CanAct"/>.
    /// </summary>
    [AvaloniaFact]
    public async Task Resuming_refuses_a_folder_that_has_not_been_walked_and_anything_that_is_not_a_card()
    {
        var world = new World();
        world.AddCourse("Un curso", @"Cursos\Un curso", watched: 1, total: 4);
        world.AddCourse("Recién marcado", @"Cursos\Recién marcado", watched: 0, total: 0);
        var model = new CoursesViewModel(world.GetCourses);
        await model.LoadAsync();

        var walked = model.Cards.Single(card => card.CanAct);
        var unwalked = model.Cards.Single(card => !card.CanAct);

        Assert.True(walked.OpenCommand.CanExecute(walked));
        Assert.True(walked.ResumeCommand.CanExecute(walked));

        // The folder nobody has walked yet opens and resumes nothing.
        Assert.True(unwalked.OpenCommand.CanExecute(unwalked));
        Assert.False(unwalked.ResumeCommand.CanExecute(unwalked));

        // And neither offer is made to something that is not a card at all.
        Assert.False(walked.OpenCommand.CanExecute(null));
        Assert.False(walked.ResumeCommand.CanExecute(null));
        Assert.False(walked.OpenCommand.CanExecute("not a card"));
        Assert.False(walked.ResumeCommand.CanExecute("not a card"));
    }

    /// <summary>
    /// A finished course opens rather than resumes, and says so on the button. This is the arm the
    /// card's own tests do not reach: they read the finished course's <c>Meta</c> and not its
    /// <c>ActionText</c>.
    /// </summary>
    [AvaloniaFact]
    public async Task A_finished_course_offers_to_open_rather_than_to_carry_on()
    {
        var world = new World();
        world.AddCourse("Terminado", @"Cursos\Terminado", watched: 4, total: 4);
        world.AddCourse("A medias", @"Cursos\A medias", watched: 1, total: 4);
        var model = new CoursesViewModel(world.GetCourses);
        await model.LoadAsync();

        var finished = model.Cards.Single(card => card.IsFinished);
        var going = model.Cards.Single(card => !card.IsFinished);

        Assert.True(finished.CanAct);
        Assert.Equal(1, finished.Progress);
        Assert.NotEqual(going.ActionText, finished.ActionText);
        Assert.NotEqual(string.Empty, finished.ActionText);
    }

    /// <summary>
    /// The card is built and pressed before anything has attached to it, which is what the whole
    /// surface does for one frame. Neither the events nor the change notification may need a
    /// listener to be safe.
    /// </summary>
    [AvaloniaFact]
    public async Task A_grid_nobody_is_listening_to_still_loads_and_still_takes_a_press()
    {
        var world = new World();
        world.AddCourse("Un curso", @"Cursos\Un curso", watched: 1, total: 4);

        // No handler on PropertyChanged, on Opened, or on Resumed.
        var model = new CoursesViewModel(world.GetCourses);
        model.MarkFolderCommand = new CountingCommand();

        await model.LoadAsync();

        var card = Assert.Single(model.Cards);
        card.OpenCommand.Execute(card);
        card.ResumeCommand.Execute(card);

        Assert.True(model.HasCourses);
    }

    /// <summary>
    /// Loading tells whoever is bound, and it tells them about the three properties a view binds to
    /// rather than only about the list.
    /// </summary>
    [AvaloniaFact]
    public async Task Loading_announces_the_list_and_both_of_the_states_read_off_it()
    {
        var world = new World();
        world.AddCourse("Un curso", @"Cursos\Un curso", watched: 1, total: 4);
        var model = new CoursesViewModel(world.GetCourses);
        var announced = new List<string?>();
        model.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        model.MarkFolderCommand = new CountingCommand();
        await model.LoadAsync();

        Assert.Contains(nameof(CoursesViewModel.MarkFolderCommand), announced, StringComparer.Ordinal);
        Assert.Contains(nameof(CoursesViewModel.Cards), announced, StringComparer.Ordinal);
        Assert.Contains(nameof(CoursesViewModel.IsEmpty), announced, StringComparer.Ordinal);
        Assert.Contains(nameof(CoursesViewModel.HasCourses), announced, StringComparer.Ordinal);
    }

    /// <summary>
    /// Where the words come from, and the two ways there can be none.
    /// </summary>
    /// <remarks>
    /// The fallbacks are not decoration: a headless test mounts these models without the string
    /// dictionaries, and this is the test that proves the three arms behave — the word is taken from
    /// the running application when it is there, and the English fallback stands in both when the key
    /// is absent and when what is under it is not a string at all.
    /// <para>
    /// This is the one place that compares against an exact string, and it is not a translation: the
    /// value asserted is the one this test just put into the dictionary itself. What a shipped string
    /// says is never pinned here.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task A_word_comes_from_the_running_dictionary_and_falls_back_when_it_cannot()
    {
        const string Key = "CourseJustMarkedStatus";
        const string Mine = "«lo que puso esta prueba»";

        var world = new World();
        world.AddCourse("Recién marcado", @"Cursos\Recién marcado", watched: 0, total: 0);
        var model = new CoursesViewModel(world.GetCourses);
        await model.LoadAsync();
        var card = Assert.Single(model.Cards);

        var application = Avalonia.Application.Current;
        Assert.NotNull(application);

        var hadKey = application.Resources.ContainsKey(Key);
        var previous = hadKey ? application.Resources[Key] : null;
        try
        {
            // No key: the fallback stands in, which is the state every other test in this suite runs
            // under because the headless application loads no string dictionaries.
            application.Resources.Remove(Key);
            var withoutAKey = card.Meta;
            Assert.NotEqual(string.Empty, withoutAKey);

            // The key is there and it is a string, so the dictionary wins.
            application.Resources[Key] = Mine;
            Assert.Equal(Mine, card.Meta);

            // The key is there and what is under it is not a string. A cast would throw at whoever is
            // drawing; the fallback stands in instead, and it is the same one the missing key gets.
            application.Resources[Key] = 42;
            Assert.Equal(withoutAKey, card.Meta);
        }
        finally
        {
            application.Resources.Remove(Key);
            if (hadKey)
            {
                application.Resources[Key] = previous;
            }
        }
    }

    /// <summary>
    /// The guard, from the side the sweep does not take. The sweep hands the constructor a null and
    /// watches it throw; this hands it the real thing and watches it carry on, and it has to happen
    /// in this suite or the merged report reads the pair as half covered for ever.
    /// </summary>
    [Fact]
    public void A_grid_needs_the_use_case_it_reads_from()
    {
        var world = new World();

        Assert.Throws<ArgumentNullException>(() => new CoursesViewModel(null!));
        Assert.True(new CoursesViewModel(world.GetCourses).IsEmpty);
    }

    /// <summary>The stores behind a real <see cref="GetCourses"/>, and the courses they hold.</summary>
    private sealed class World
    {
        private readonly StubCourses _courses = new();
        private readonly StubLessons _lessons = new();

        public World() => GetCourses = new GetCourses(_courses, _lessons);

        public GetCourses GetCourses { get; }

        /// <summary>
        /// A course of <paramref name="total"/> lessons with the first <paramref name="watched"/> of
        /// them watched, which is what decides every state a card can be in. A course of no lessons
        /// is a folder marked and not yet walked.
        /// </summary>
        public CourseId AddCourse(string title, string relativePath, int watched, int total)
        {
            var id = new CourseId(Guid.NewGuid());
            _courses.Courses.Add(new Course(
                id,
                new LibraryRootId(Guid.NewGuid()),
                relativePath,
                title,
                new DateTimeOffset(2026, 8, 1, 9, 0, 0, TimeSpan.Zero),
                null));

            if (total > 0)
            {
                _lessons.ByCourse[id] =
                [
                    .. Enumerable.Range(1, total).Select(number => new CourseLessonProgress(
                        new LessonId(Guid.NewGuid()),
                        new MediaFileId(Guid.NewGuid()),
                        1,
                        "Introducción",
                        number,
                        $"Lección {number}",
                        TimeSpan.FromMinutes(10),
                        TimeSpan.Zero,
                        number <= watched ? WatchStatus.Watched : WatchStatus.NotStarted)),
                ];
            }

            return id;
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

    private sealed class StubLessons : ICourseLessonReader
    {
        public Dictionary<CourseId, IReadOnlyList<CourseLessonProgress>> ByCourse { get; } = [];

        public Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ByCourse.TryGetValue(courseId, out var lessons) ? lessons : []);

        public Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>>(ByCourse);
    }

    private sealed class CountingCommand : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
