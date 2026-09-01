// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Courses;

/// <summary>
/// The lesson chain (CRS-004): which lesson comes next, the wait that PLY-011 owns, and the file
/// confirmed at zero rather than trusted from when the offer was made.
/// </summary>
public sealed class NextLessonCountdownTests
{
    [Fact]
    public async Task The_countdown_announces_every_second_and_then_opens_the_next_lesson()
    {
        var harness = new Harness();

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Started, result.Outcome);
        Assert.Equal("El nodo", result.Lesson?.Title);
        Assert.Equal([10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0], harness.Announced);
        Assert.Single(harness.Coordinator.Requests);
    }

    /// <summary>
    /// The offer names the lesson before any wait begins, which is what the overlay writes. It is a
    /// separate call precisely so that naming it costs nothing when the chain is switched off.
    /// </summary>
    [Fact]
    public async Task The_next_lesson_can_be_named_without_starting_anything()
    {
        var harness = new Harness();

        var candidate = await harness.Countdown.PeekAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal("El nodo", candidate?.Title);
        Assert.Empty(harness.Announced);
        Assert.Empty(harness.Coordinator.Requests);
    }

    /// <summary>«Curso terminado»: the shell goes back to the card rather than chaining on.</summary>
    [Fact]
    public async Task The_last_lesson_of_a_course_offers_nothing()
    {
        var harness = new Harness();

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(2),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.NoNextEpisode, result.Outcome);
        Assert.Null(result.Lesson);
        Assert.Empty(harness.Coordinator.Requests);
    }

    /// <summary>A file that is not a lesson at all never reaches this chain with anything to offer.</summary>
    [Fact]
    public async Task A_file_that_is_not_a_lesson_offers_nothing()
    {
        var harness = new Harness();

        var result = await harness.Countdown.ExecuteAsync(
            new MediaFileId(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.NoNextEpisode, result.Outcome);
    }

    /// <summary>Zero is the off switch, and it is the same stored setting the episode chain reads.</summary>
    [Fact]
    public async Task Zero_seconds_switches_the_chain_off_without_offering_anything()
    {
        var harness = new Harness();
        harness.Settings.Write(ContinuityCountdown.SettingKey, 0);

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Disabled, result.Outcome);
        Assert.Empty(harness.Coordinator.Requests);
        Assert.Empty(harness.Coordinator.Requests);
    }

    /// <summary>
    /// The length a person chose applies to both chains, because it is one setting and one object.
    /// A second key would have left every existing installation's choice behind on the old one.
    /// </summary>
    [Fact]
    public void The_configured_length_is_the_one_the_episode_chain_stores()
    {
        var harness = new Harness();
        harness.Settings.Write(StartNextEpisodeCountdown.SettingKey, 25);

        Assert.Equal(25, harness.Countdown.CountdownSeconds);
        Assert.Equal(ContinuityCountdown.SettingKey, StartNextEpisodeCountdown.SettingKey);
    }

    [Fact]
    public async Task Cancelling_the_wait_opens_nothing()
    {
        var harness = new Harness();
        harness.Clock.CancelAfter(3, harness.Countdown.Cancel);

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Cancelled, result.Outcome);
        Assert.Equal("El nodo", result.Lesson?.Title);
        Assert.Empty(harness.Coordinator.Requests);
    }

    /// <summary>
    /// The drive pulled out while the countdown ran. The file is re-read at zero, so it is found now
    /// rather than trusted from when the offer was made — T28's rule, kept by the course chain.
    /// </summary>
    [Fact]
    public async Task A_lesson_whose_file_disappears_during_the_wait_is_never_opened()
    {
        var harness = new Harness();
        var vanishing = harness.FileOf(1);
        harness.Clock.CancelAfter(4, () => harness.Files.Forget(vanishing));

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Unavailable, result.Outcome);
        Assert.Empty(harness.Coordinator.Requests);
    }

    /// <summary>
    /// The identity still resolves at zero but to a row with no path — the shape of a corrupt
    /// catalogue entry. It is reported rather than handed to the engine, which is what the second
    /// half of the guard is for: «the file is still there» and «the file can be opened» are two
    /// questions, and only the first one a re-read answers.
    /// </summary>
    [Fact]
    public async Task A_lesson_whose_catalogued_file_has_no_path_is_reported_as_unavailable()
    {
        var harness = new Harness();
        harness.Clock.CancelAfter(4, () => harness.Files.Replace(harness.FileOf(1), pathless: true));

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Unavailable, result.Outcome);
        Assert.Empty(harness.Coordinator.Requests);
    }

    /// <summary>An engine that refuses the file is reported, never dressed up as a start.</summary>
    [Fact]
    public async Task A_lesson_the_engine_refuses_is_reported_as_unavailable()
    {
        var harness = new Harness();
        harness.Coordinator.FailureOnStart = new PlaybackFailure(PlaybackFailureCode.OpenFailed, "no");

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Unavailable, result.Outcome);
    }

    /// <summary>
    /// The countdown never leaves two sessions running, which is the coordinator's own promise and
    /// the reason the chain goes through it rather than opening anything itself.
    /// </summary>
    [Fact]
    public async Task Chained_lessons_never_hold_two_sessions()
    {
        var harness = new Harness();

        _ = await harness.Countdown.ExecuteAsync(harness.FileOf(0), TestContext.Current.CancellationToken);
        _ = await harness.Countdown.ExecuteAsync(harness.FileOf(1), TestContext.Current.CancellationToken);

        Assert.Equal(2, harness.Coordinator.Requests.Count);
        Assert.Equal(1, harness.Coordinator.MaximumConcurrentSessions);
    }

    /// <summary>
    /// Nobody subscribed to the chain's own <c>Ticked</c>, which is the arm the composition root
    /// never takes because it attaches a handler before every offer. It is a real state all the
    /// same — the countdown runs whether or not an overlay is listening — and a null-conditional
    /// invoke that threw here would take the session down with it.
    /// </summary>
    [Fact]
    public async Task A_chain_nobody_is_listening_to_still_opens_the_next_lesson()
    {
        var harness = new Harness(listen: false);

        var result = await harness.Countdown.ExecuteAsync(
            harness.FileOf(0),
            TestContext.Current.CancellationToken);

        Assert.Equal(NextEpisodeOutcome.Started, result.Outcome);
        Assert.Empty(harness.Announced);
        Assert.Single(harness.Coordinator.Requests);
    }

    private sealed class Harness
    {
        private readonly StubCourseStore _courses = new();

        public Harness(bool listen = true)
        {
            Files = new StubMediaFiles();
            var lessons = new List<Lesson>();
            var progress = new List<CourseLessonProgress>();
            var titles = new[] { "Intro", "El nodo", "Máscaras" };
            for (var index = 0; index < titles.Length; index++)
            {
                var fileId = Files.Add($@"D:\Cursos\Compositing\{index + 1:D2} {titles[index]}.mkv");
                var lessonId = new LessonId(Guid.NewGuid());
                lessons.Add(new Lesson(
                    lessonId,
                    _courses.CourseId,
                    fileId,
                    "Fundamentos",
                    new LessonOrdinal(1, null),
                    new LessonOrdinal(index + 1, null),
                    $"{index + 1:D2} {titles[index]}.mkv",
                    titles[index],
                    $@"01 Fundamentos\{index + 1:D2} {titles[index]}.mkv"));
                progress.Add(new CourseLessonProgress(
                    lessonId,
                    fileId,
                    1,
                    "Fundamentos",
                    index + 1,
                    titles[index],
                    TimeSpan.FromMinutes(10),
                    TimeSpan.Zero,
                    WatchStatus.NotStarted));
            }

            _courses.Lessons = lessons;
            _courses.Progress = progress;
            Coordinator = new CountingCoordinator();
            Clock = new SteppingClock();
            Countdown = new StartNextLessonCountdown(
                new GetLessonSession(_courses, new GetCourses(_courses, _courses)),
                Files,
                Coordinator,
                Settings,
                Clock);
            if (listen)
            {
                Countdown.Ticked += (_, remaining) => Announced.Add(remaining);
            }
        }

        public StubMediaFiles Files { get; }

        public InMemorySettings Settings { get; } = new();

        public CountingCoordinator Coordinator { get; }

        public SteppingClock Clock { get; }

        public StartNextLessonCountdown Countdown { get; }

        public List<int> Announced { get; } = [];

        public MediaFileId FileOf(int index) => _courses.Progress[index].MediaFileId!.Value;
    }

    /// <summary>
    /// One course, answering the three ports the session reader needs. Written here rather than in
    /// the shared stubs because those deliberately refuse everything the folder walk does not use,
    /// and making them answer would hide a call somebody did not mean to make.
    /// </summary>
    private sealed class StubCourseStore : ICourseRepository, ICourseLessonReader
    {
        public CourseId CourseId { get; } = new(Guid.NewGuid());

        public IReadOnlyList<Lesson> Lessons { get; set; } = [];

        public List<CourseLessonProgress> Progress { get; set; } = [];

        public Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Course?>(id == CourseId
                ? new Course(CourseId, default, "Compositing", "Compositing", default, null)
                : null);

        public Task<Lesson?> FindLessonByFileAsync(
            MediaFileId fileId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Lessons.FirstOrDefault(lesson => lesson.MediaFileId == fileId));

        public Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CourseLessonProgress>>(Progress);

        public Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CourseId> SaveAsync(
            Course course,
            IReadOnlyList<Lesson> lessons,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Lesson>> ListLessonsAsync(
            CourseId courseId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task TouchAsync(
            CourseId id,
            DateTimeOffset openedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InMemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    /// <summary>
    /// A clock that returns immediately, counting the ticks and running whatever was scheduled for a
    /// given one.
    /// </summary>
    /// <remarks>
    /// The episode suite drives its wait from the outside with a manual clock, which is what it needs
    /// to assert that each second is announced <i>as it passes</i>. Here the wait is the very object
    /// that suite already exercises, so what these tests are for is the two ends around it — and a
    /// clock that hands the loop back immediately makes them run in milliseconds instead of ten
    /// seconds each.
    /// </remarks>
    private sealed class SteppingClock : IClock
    {
        private readonly Dictionary<int, Action> _scheduled = [];
        private int _elapsed;

        public DateTimeOffset UtcNow { get; } = new(2026, 9, 1, 20, 0, 0, TimeSpan.Zero);

        public void CancelAfter(int ticks, Action action) => _scheduled[ticks] = action;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            _elapsed++;
            if (_scheduled.TryGetValue(_elapsed, out var action))
            {
                action();
            }

            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask;
        }
    }

    private sealed class CountingCoordinator : IPlaybackSessionCoordinator
    {
        private int _active;

        public PlaybackSession? ActiveSession { get; private set; }

        public PlaybackFailure? FailureOnStart { get; set; }

        public List<PlaybackRequest> Requests { get; } = [];

        public int MaximumConcurrentSessions { get; private set; }

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (FailureOnStart is { } failure)
            {
                throw new PlaybackFailureException(failure);
            }

            _active = 1;
            MaximumConcurrentSessions = Math.Max(MaximumConcurrentSessions, _active);
            Requests.Add(request);
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _active = 0;
            ActiveSession = null;
            return Task.CompletedTask;
        }
    }
}
