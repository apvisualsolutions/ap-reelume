// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Courses;
using ApSolutions.LocalMedia.Presentation.Movie;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// One lesson in the player's side column (CRS-004): a glyph, a name, and how long it runs.
/// </summary>
/// <remarks>
/// Deliberately not <c>LessonRowViewModel</c>, which is the card's row. That one carries a mark
/// button, a progress bar, a «Siguiente en el hilo» chip and a two-part meta line — 320 px of column
/// beside a picture is not where any of that goes, and the prototype draws exactly three things per
/// row here. Sharing the type would have meant a row with half its bindings hidden, which is how a
/// surface ends up drawing a control nobody can reach.
/// </remarks>
public sealed class LessonsPanelRowViewModel
{
    private readonly CourseLessonProgress _lesson;

    public LessonsPanelRowViewModel(CourseLessonProgress lesson, bool isCurrent, ICommand play)
    {
        _lesson = lesson ?? throw new ArgumentNullException(nameof(lesson));
        IsCurrent = isCurrent;
        PlayCommand = play ?? throw new ArgumentNullException(nameof(play));
    }

    public LessonId Id => _lesson.Id;

    public Domain.Catalog.MediaFileId? MediaFileId => _lesson.MediaFileId;

    public TimeSpan Position => _lesson.Position;

    public string Title => _lesson.Title;

    /// <summary>
    /// The panel's own command, shared by every row: the whole row is the button, which is what the
    /// prototype draws and what a 320 px column has space for.
    /// </summary>
    public ICommand PlayCommand { get; }

    /// <summary>
    /// The row the session is on, which the prototype draws with a filled background and a border.
    /// </summary>
    public bool IsCurrent { get; }

    /// <summary>
    /// The three states as shapes, and the lesson being played reads as started even before any
    /// progress has been written for it — which is what the prototype's <c>partial || curNow</c>
    /// says and what somebody looking at the column would otherwise find missing: the row they are
    /// watching drawn as never started.
    /// </summary>
    public string Glyph => _lesson.Status switch
    {
        WatchStatus.Watched => "●",
        WatchStatus.InProgress => "◐",
        _ => IsCurrent ? "◐" : "○",
    };

    /// <summary>«24 min», or nothing at all when the catalogue has no duration for the file.</summary>
    public string Duration => _lesson.Duration > TimeSpan.Zero
        ? CourseText.Resource("CatalogRuntimeMinutes", "{0} min").Replace(
            "{0}",
            ((int)_lesson.Duration.TotalMinutes).ToString(CultureInfo.CurrentCulture),
            StringComparison.Ordinal)
        : string.Empty;

    public bool HasDuration => Duration.Length > 0;

    /// <summary>A lesson whose file the catalogue has not seen refuses rather than fails.</summary>
    public bool CanPlay => _lesson.MediaFileId is not null;

    /// <summary>
    /// The state in words, because the glyph is hidden from the automation tree: a shape read out as
    /// «black circle» is not what the row means, and read beside the sentence it is read twice.
    /// </summary>
    public string AccessibleName
    {
        get
        {
            var state = _lesson.Status switch
            {
                WatchStatus.Watched => CourseText.Resource("WatchStatusWatched", "Watched"),
                WatchStatus.InProgress => CourseText.Resource("WatchStatusInProgress", "In progress"),
                _ => IsCurrent
                    ? CourseText.Resource("WatchStatusInProgress", "In progress")
                    : CourseText.Resource("WatchStatusNotStarted", "Not started"),
            };

            return HasDuration
                ? string.Create(CultureInfo.CurrentCulture, $"{Title}. {Duration}. {state}")
                : string.Create(CultureInfo.CurrentCulture, $"{Title}. {state}");
        }
    }
}

/// <summary>A module and its lessons, as the column stacks them.</summary>
public sealed class LessonsPanelModuleViewModel
{
    public LessonsPanelModuleViewModel(
        int number,
        string? title,
        IReadOnlyList<LessonsPanelRowViewModel> lessons)
    {
        Lessons = lessons ?? throw new ArgumentNullException(nameof(lessons));
        HasLabel = title is not null;
        Label = title is null
            ? string.Empty
            : CourseText.Resource("CourseModuleFormat", "Module {0} · {1}")
                .Replace("{0}", number.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
                .Replace("{1}", title, StringComparison.Ordinal);
    }

    /// <summary>
    /// False for the lessons loose in the course folder. Drawing «Módulo 1» over them would invent a
    /// module, which is the same decision the card made.
    /// </summary>
    public bool HasLabel { get; }

    public string Label { get; }

    public IReadOnlyList<LessonsPanelRowViewModel> Lessons { get; }
}

/// <summary>
/// The player's «Lecciones» panel (CRS-004): the whole course beside the picture, with the lesson
/// being played marked and every other one a press away.
/// </summary>
/// <remarks>
/// <b>It exists only during a lesson session.</b> The shell builds it when the file that opened
/// turns out to be a lesson and leaves it null otherwise, which is what makes the pill and the
/// column <b>absent</b> rather than disabled — a disabled «Lecciones» beside a film would be a
/// promise that the film has lessons somewhere.
/// </remarks>
public sealed class LessonsPanelViewModel : INotifyPropertyChanged
{
    private readonly Func<PlayDetailsRequest, Task>? _onPlay;
    private readonly AsyncRelayCommand _play;
    private LessonSession _session;

    public LessonsPanelViewModel(LessonSession session, Func<PlayDetailsRequest, Task>? onPlay = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _onPlay = onPlay;
        _play = new AsyncRelayCommand(
            parameter => PlayAsync((LessonsPanelRowViewModel)parameter!),
            parameter => parameter is LessonsPanelRowViewModel { CanPlay: true });
        Rebuild();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand PlayCommand => _play;

    /// <summary>The course this session belongs to, which the countdown asks for by name.</summary>
    public CourseId CourseId => _session.Course.Id;

    /// <summary>The lesson being played.</summary>
    public LessonId LessonId => _session.LessonId;

    public string CourseTitle => _session.Course.Title;

    /// <summary>«7/24 lecciones · 6 h 30 min restantes», and only the first half once nothing is left.</summary>
    public string Head { get; private set; } = string.Empty;

    public IReadOnlyList<LessonsPanelModuleViewModel> Modules { get; private set; } = [];

    /// <summary>
    /// The promise under the list, which is the panel's whole reason for being trusted: nobody has to
    /// remember where they were.
    /// </summary>
    public static string Note => CourseText.Resource(
        "PlayerLessonsThreadNote",
        "The thread keeps itself: position is written every 5 seconds, per lesson.");

    /// <summary>
    /// Re-reads the course around the same lesson, for when a mark or a write has moved the counts.
    /// </summary>
    public void Update(LessonSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Rebuild();
    }

    private void Rebuild()
    {
        var summary = _session.Course.Summary;
        var watched = CourseText.Resource("CourseLessonsFormat", "{0}/{1} lessons")
            .Replace("{0}", summary.WatchedLessons.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal)
            .Replace("{1}", summary.TotalLessons.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

        // The prototype hangs the remaining time off the thread existing, not off the arithmetic:
        // a finished course says «24/24 lecciones» and stops. Written the other way round — on
        // whether the remainder is zero — a course whose last lesson has no duration in the
        // catalogue would say «0 min restantes», which reads as finished when it is not.
        Head = summary.IsFinished
            ? watched
            : watched + " · " + CourseText.Resource("CourseRemainingFormat", "{0} left")
                .Replace("{0}", CourseText.Duration(summary.Remaining), StringComparison.Ordinal);

        Modules =
        [
            .. _session.Course.Modules.Select(module => new LessonsPanelModuleViewModel(
                module.Number,
                module.Title,
                [
                    .. module.Lessons.Select(lesson => new LessonsPanelRowViewModel(
                        lesson,
                        lesson.Id == _session.LessonId,
                        _play)),
                ])),
        ];

        OnPropertyChanged(nameof(Head));
        OnPropertyChanged(nameof(Modules));
        OnPropertyChanged(nameof(CourseTitle));
    }

    private Task PlayAsync(LessonsPanelRowViewModel lesson) => _onPlay is { } play
        ? play(new PlayDetailsRequest(
            lesson.MediaFileId,
            lesson.Position > TimeSpan.Zero ? lesson.Position : null,
            CourseTitle,
            lesson.Title))
        : Task.CompletedTask;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
