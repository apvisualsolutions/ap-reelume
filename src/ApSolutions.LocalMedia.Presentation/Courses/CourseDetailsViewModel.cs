// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Movie;

namespace ApSolutions.LocalMedia.Presentation.Courses;

/// <summary>
/// One lesson on the course card (CRS-005), the mirror of an episode row: the same three states, the
/// same glyphs, the same partial bar, and a mark that a person's hand wins with.
/// </summary>
public sealed class LessonRowViewModel
{
    private readonly CourseLessonProgress _lesson;

    public LessonRowViewModel(
        CourseLessonProgress lesson,
        bool isNextInThread,
        ICommand play,
        ICommand toggleWatched)
    {
        _lesson = lesson ?? throw new ArgumentNullException(nameof(lesson));
        IsNextInThread = isNextInThread;
        PlayCommand = play ?? throw new ArgumentNullException(nameof(play));
        ToggleWatchedCommand = toggleWatched ?? throw new ArgumentNullException(nameof(toggleWatched));
    }

    public LessonId Id => _lesson.Id;

    public Domain.Catalog.MediaFileId? MediaFileId => _lesson.MediaFileId;

    public TimeSpan Position => _lesson.Position;

    public bool IsWatched => _lesson.Status == WatchStatus.Watched;

    public bool IsNextInThread { get; }

    public ICommand PlayCommand { get; }

    public ICommand ToggleWatchedCommand { get; }

    /// <summary>
    /// The three states as shapes and not as colours: an empty ring, a half-filled one, a full one.
    /// Somebody who cannot tell the accent from the secondary text still reads all three.
    /// </summary>
    public string Glyph => _lesson.Status switch
    {
        WatchStatus.Watched => "●",
        WatchStatus.InProgress => "◐",
        _ => "○",
    };

    /// <summary>«L06», numbered through the course rather than through the module.</summary>
    public string Number => string.Create(CultureInfo.CurrentCulture, $"L{_lesson.Number:00}");

    public string Title => _lesson.Title;

    /// <summary>«24 min · Reanudar en 4:00», the same sentence an episode row writes.</summary>
    public string Meta
    {
        get
        {
            var minutes = _lesson.Duration > TimeSpan.Zero
                ? CourseText.Resource("CatalogRuntimeMinutes", "{0} min").Replace(
                    "{0}",
                    ((int)_lesson.Duration.TotalMinutes).ToString(CultureInfo.CurrentCulture),
                    StringComparison.Ordinal) + " · "
                : string.Empty;

            return minutes + _lesson.Status switch
            {
                WatchStatus.Watched => CourseText.Resource("WatchStatusWatched", "Watched"),
                WatchStatus.InProgress => CourseText.Resource("EpisodeResumeAt", "Resume at {0}").Replace(
                    "{0}",
                    Player.PlaybackClock.Format(_lesson.Position),
                    StringComparison.Ordinal),
                _ => CourseText.Resource("WatchStatusNotStarted", "Not started"),
            };
        }
    }

    public bool HasBar => _lesson.Status == WatchStatus.InProgress && _lesson.Duration > TimeSpan.Zero;

    public double Progress => _lesson.Duration > TimeSpan.Zero
        ? Math.Clamp(_lesson.Position.TotalSeconds / _lesson.Duration.TotalSeconds, 0, 1)
        : 0;

    /// <summary>
    /// A lesson whose file the catalogue has not seen cannot be played, and the row says so by
    /// refusing rather than by failing when pressed.
    /// </summary>
    public bool CanPlay => _lesson.MediaFileId is not null;

    public string MarkActionText => IsWatched
        ? CourseText.Resource("CourseUnmarkAction", "Unmark")
        : CourseText.Resource("CourseMarkWatchedAction", "Mark as watched");

    public string AccessibleName =>
        string.Create(CultureInfo.CurrentCulture, $"{Number} {Title}. {Meta}");

    /// <summary>What the mark button announces, which has to name the lesson it is on.</summary>
    public string MarkAccessibleName =>
        string.Create(CultureInfo.CurrentCulture, $"{MarkActionText}. {Number} {Title}");
}

/// <summary>A module and its lessons, as the card stacks them.</summary>
public sealed class CourseModuleViewModel
{
    public CourseModuleViewModel(CourseModuleView module, IReadOnlyList<LessonRowViewModel> lessons)
    {
        ArgumentNullException.ThrowIfNull(module);
        Lessons = lessons ?? throw new ArgumentNullException(nameof(lessons));
        HasLabel = module.Title is not null;
        Label = module.Title is null
            ? string.Empty
            : CourseText.Resource("CourseModuleFormat", "Module {0} · {1}")
                .Replace("{0}", module.Number.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace("{1}", module.Title, StringComparison.Ordinal);
        Count = string.Create(CultureInfo.CurrentCulture, $"{module.WatchedLessons}/{module.Lessons.Count}");
    }

    /// <summary>
    /// False for the lessons loose in the course folder: they are not a module, and drawing
    /// «Módulo 1» over them would invent one.
    /// </summary>
    public bool HasLabel { get; }

    public string Label { get; }

    public string Count { get; }

    public IReadOnlyList<LessonRowViewModel> Lessons { get; }
}

/// <summary>
/// A course opened (CRS-002, CRS-003): its header, its modules, and the thread panel that answers
/// «¿por dónde iba?» without making anybody re-watch anything.
/// </summary>
public sealed class CourseDetailsViewModel : INotifyPropertyChanged
{
    private readonly GetCourses _getCourses;
    private readonly SetWatchStatus _setWatchStatus;
    private readonly AsyncRelayCommand _play;
    private readonly AsyncRelayCommand _toggleWatched;
    private readonly AsyncRelayCommand _resumeThread;
    private CourseDetail? _detail;

    public CourseDetailsViewModel(GetCourses getCourses, SetWatchStatus setWatchStatus)
    {
        _getCourses = getCourses ?? throw new ArgumentNullException(nameof(getCourses));
        _setWatchStatus = setWatchStatus ?? throw new ArgumentNullException(nameof(setWatchStatus));
        _play = new AsyncRelayCommand(
            parameter => PlayAsync((LessonRowViewModel)parameter!),
            parameter => parameter is LessonRowViewModel { CanPlay: true });
        _toggleWatched = new AsyncRelayCommand(
            parameter => ToggleWatchedAsync((LessonRowViewModel)parameter!),
            parameter => parameter is LessonRowViewModel { CanPlay: true });
        _resumeThread = new AsyncRelayCommand(ResumeThreadAsync, () => ThreadLessonRow is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// A lesson was asked for. The shell owns the player, so the card asks rather than opens — the
    /// same division the film card already uses.
    /// </summary>
    public Func<PlayDetailsRequest, Task>? PlayRequested { get; set; }

    public ICommand PlayCommand => _play;

    public ICommand ToggleWatchedCommand => _toggleWatched;

    public ICommand ResumeThreadCommand => _resumeThread;

    public bool HasCourse => _detail is not null;

    public string Title => _detail?.Title ?? string.Empty;

    public string RelativePath => _detail?.RelativePath ?? string.Empty;

    /// <summary>«3 módulos · 24 lecciones · 8 h 10 min».</summary>
    public string Meta => _detail is null
        ? string.Empty
        : CourseText.Resource("CourseMetaFormat", "{0} modules · {1} lessons · {2}")
            .Replace("{0}", _detail.ModuleCount.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
            .Replace(
                "{1}",
                _detail.Summary.TotalLessons.ToString(CultureInfo.CurrentCulture),
                StringComparison.Ordinal)
            .Replace("{2}", CourseText.Duration(_detail.TotalDuration), StringComparison.Ordinal);

    /// <summary>«7/24 vistas · 6 h 30 min restantes», and only the first half once it is finished.</summary>
    public string ProgressText
    {
        get
        {
            if (_detail is null)
            {
                return string.Empty;
            }

            var watched = CourseText.Resource("CourseWatchedFormat", "{0}/{1} watched")
                .Replace(
                    "{0}",
                    _detail.Summary.WatchedLessons.ToString(CultureInfo.CurrentCulture),
                    StringComparison.Ordinal)
                .Replace(
                    "{1}",
                    _detail.Summary.TotalLessons.ToString(CultureInfo.CurrentCulture),
                    StringComparison.Ordinal);

            return _detail.Summary.IsFinished
                ? watched
                : watched + " · " + CourseText.Resource("CourseRemainingFormat", "{0} left")
                    .Replace("{0}", CourseText.Duration(_detail.Summary.Remaining), StringComparison.Ordinal);
        }
    }

    public double Progress => _detail is { Summary.TotalLessons: > 0 } detail
        ? (double)detail.Summary.WatchedLessons / detail.Summary.TotalLessons
        : 0;

    public bool IsFinished => _detail is { } detail && detail.Summary.IsFinished;

    /// <summary>«M2 · El canal alfa», or nothing left to point at.</summary>
    public string ThreadLesson => _detail is null || _detail.Thread.IsCourseFinished
        ? string.Empty
        : string.Create(
            CultureInfo.CurrentCulture,
            $"M{_detail.Thread.ModuleNumber} · {_detail.Thread.LessonTitle}");

    /// <summary>«Reanudar en 4:00 / 24:00», or nothing when the lesson was never started.</summary>
    public string ThreadMinute => _detail is { Thread.IsPartial: true } detail
        ? CourseText.Resource("CourseResumeAtFormat", "Resume at {0} / {1}")
            .Replace("{0}", Player.PlaybackClock.Format(detail.Thread.Position), StringComparison.Ordinal)
            .Replace("{1}", Player.PlaybackClock.Format(detail.Thread.Duration), StringComparison.Ordinal)
        : string.Empty;

    public bool HasThreadMinute => ThreadMinute.Length > 0;

    /// <summary>
    /// «Retomar el hilo» while anything is left, «Volver a empezar» once nothing is. One label read
    /// off one flag, so the button and the finished chip can never disagree.
    /// </summary>
    public string ThreadActionText => IsFinished
        ? CourseText.Resource("CourseThreadRestartCta", "Watch again from the start")
        : CourseText.Resource("CourseThreadCta", "Pick up the thread");

    public IReadOnlyList<string> Recap { get; private set; } = [];

    public bool HasRecap => Recap.Count > 0;

    public IReadOnlyList<CourseModuleViewModel> Modules { get; private set; } = [];

    /// <summary>The row the thread points at, which is what the panel's own button plays.</summary>
    public LessonRowViewModel? ThreadLessonRow { get; private set; }

    /// <summary>
    /// What the last mark did, announced rather than only drawn (CRS-005).
    /// </summary>
    /// <remarks>
    /// Marking a lesson moves the thread, and the thread is the whole point of the card. Somebody
    /// reading with their eyes sees the chip jump to the next row; somebody reading with a screen
    /// reader would get nothing at all, because a glyph that changed somewhere else on the page is
    /// not an announcement. So it is said in words, in one live region, and cleared on every load so
    /// that reopening a course never re-announces a mark from last time.
    /// </remarks>
    public string MarkNotice { get; private set; } = string.Empty;

    public bool HasMarkNotice => MarkNotice.Length > 0;

    public async Task LoadAsync(CourseId courseId, CancellationToken cancellationToken = default)
    {
        MarkNotice = string.Empty;
        _detail = await _getCourses.GetAsync(courseId, cancellationToken).ConfigureAwait(true);
        Rebuild();
    }

    private void Rebuild()
    {
        if (_detail is null)
        {
            Modules = [];
            Recap = [];
            ThreadLessonRow = null;
        }
        else
        {
            var thread = _detail.Thread.Lesson;
            Modules =
            [
                .. _detail.Modules.Select(module => new CourseModuleViewModel(
                    module,
                    [
                        .. module.Lessons.Select(lesson => new LessonRowViewModel(
                            lesson,
                            lesson.Id == thread,
                            _play,
                            _toggleWatched)),
                    ])),
            ];
            Recap =
            [
                .. _detail.Recap.Select(lesson => string.Create(
                    CultureInfo.CurrentCulture,
                    $"M{lesson.ModuleNumber} · {lesson.Title}")),
            ];

            // The finished course keeps a row to play: «Volver a empezar» starts at the first lesson,
            // and a button with nothing behind it would be a button that looks pressable and is not.
            ThreadLessonRow = Modules
                .SelectMany(module => module.Lessons)
                .FirstOrDefault(row => row.IsNextInThread)
                ?? Modules.SelectMany(module => module.Lessons).FirstOrDefault();
        }

        foreach (var name in new[]
        {
            nameof(HasCourse), nameof(Title), nameof(RelativePath), nameof(Meta), nameof(ProgressText),
            nameof(Progress), nameof(IsFinished), nameof(ThreadLesson), nameof(ThreadMinute),
            nameof(HasThreadMinute), nameof(ThreadActionText), nameof(Recap), nameof(HasRecap),
            nameof(Modules), nameof(ThreadLessonRow), nameof(MarkNotice), nameof(HasMarkNotice),
        })
        {
            OnPropertyChanged(name);
        }

        _resumeThread.RaiseCanExecuteChanged();
    }

    private Task PlayAsync(LessonRowViewModel lesson) => PlayRequested is { } play
        ? play(new PlayDetailsRequest(
            lesson.MediaFileId,
            lesson.Position > TimeSpan.Zero ? lesson.Position : null,
            Title,
            lesson.Title))
        : Task.CompletedTask;

    private Task ResumeThreadAsync() => ThreadLessonRow is { } row ? PlayAsync(row) : Task.CompletedTask;

    /// <summary>
    /// Marks the lesson watched, or hands it back. It writes through PLY-009's own use case and the
    /// key PLY-008 already stores under, so a lesson's mark is a watch state like any other — which
    /// is what makes it survive the file being moved.
    /// </summary>
    private async Task ToggleWatchedAsync(LessonRowViewModel lesson)
    {
        if (_detail is null || lesson.MediaFileId is not { } file)
        {
            return;
        }

        var marking = !lesson.IsWatched;
        var courseId = _detail.Id;
        await _setWatchStatus.MarkAsync(
            CourseProgressKey.For(courseId, lesson.Id),
            file,
            marking ? WatchStatus.Watched : WatchStatus.NotStarted,
            CancellationToken.None).ConfigureAwait(true);
        await LoadAsync(courseId, CancellationToken.None).ConfigureAwait(true);

        // Said after the reload, not before: the sentence claims the thread moved, and it has only
        // moved once the card has been read again.
        MarkNotice = marking
            ? CourseText.Resource("CourseMarkedNotice", "Lesson marked as watched.")
            : CourseText.Resource("CourseUnmarkedNotice", "Mark removed.");
        OnPropertyChanged(nameof(MarkNotice));
        OnPropertyChanged(nameof(HasMarkNotice));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
