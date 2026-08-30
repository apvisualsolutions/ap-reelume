// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Courses;

/// <summary>
/// One course in the grid (CRS-003): what it is, how far in it is, and the one button that carries
/// on with it.
/// </summary>
public sealed class CourseCardViewModel
{
    private readonly CourseCard _card;

    public CourseCardViewModel(CourseCard card, ICommand open, ICommand resume)
    {
        _card = card ?? throw new ArgumentNullException(nameof(card));
        OpenCommand = open ?? throw new ArgumentNullException(nameof(open));
        ResumeCommand = resume ?? throw new ArgumentNullException(nameof(resume));
    }

    public CourseId Id => _card.Id;

    public string Title => _card.Title;

    /// <summary>The folder, written in the monospace face the tree already uses for a path.</summary>
    public string RelativePath => _card.RelativePath;

    public ICommand OpenCommand { get; }

    public ICommand ResumeCommand { get; }

    /// <summary>
    /// «3/12 lecciones · 2 h 10 min restantes», and only the first half once nothing is left.
    /// </summary>
    public string Meta
    {
        get
        {
            if (_card.Summary.IsEmpty)
            {
                return CourseText.Resource("CourseJustMarkedStatus", "Just marked");
            }

            var lessons = CourseText
                .Resource("CourseLessonsFormat", "{0}/{1} lessons")
                .Replace("{0}", Number(_card.Summary.WatchedLessons), StringComparison.Ordinal)
                .Replace("{1}", Number(_card.Summary.TotalLessons), StringComparison.Ordinal);

            return _card.Summary.IsFinished
                ? lessons
                : lessons + " · " + CourseText
                    .Resource("CourseRemainingFormat", "{0} left")
                    .Replace("{0}", CourseText.Duration(_card.Summary.Remaining), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// What the card's own button says. Four labels and every one of them is a different offer:
    /// a folder still being walked opens nothing, a finished course opens rather than resumes, and
    /// picking up is not the same word as continuing — the prototype separates them by whether the
    /// lesson the thread points at was already started.
    /// </summary>
    public string ActionText
    {
        get
        {
            if (_card.Summary.IsEmpty)
            {
                return CourseText.Resource("CourseOpensWhenScanned", "Opens when scanned");
            }

            if (_card.Thread.IsCourseFinished)
            {
                return CourseText.Resource("CourseFinishedOpenAction", "Finished · open");
            }

            var key = _card.Thread.IsPartial ? "CoursePickUpFormat" : "CourseContinueFormat";
            return CourseText
                .Resource(key, "{0}")
                .Replace("{0}", CourseText.Coordinates(_card.Thread), StringComparison.Ordinal);
        }
    }

    /// <summary>Whether the card's button leads anywhere. A folder still being walked does not.</summary>
    public bool CanAct => !_card.Summary.IsEmpty;

    public bool IsFinished => _card.Thread.IsCourseFinished && !_card.Summary.IsEmpty;

    /// <summary>
    /// How much of the bar is filled, from 0 to 1. A course with no lessons reads as zero rather
    /// than dividing by none.
    /// </summary>
    public double Progress => _card.Summary.TotalLessons == 0
        ? 0
        : (double)_card.Summary.WatchedLessons / _card.Summary.TotalLessons;

    /// <summary>What a screen reader says instead of reading four labels in a row.</summary>
    public string AccessibleName =>
        string.Create(CultureInfo.CurrentCulture, $"{Title}. {Meta}");

    private static string Number(int value) => value.ToString(CultureInfo.CurrentCulture);
}

/// <summary>
/// The courses destination (CRS-003): the grid, and the positive empty state that offers to mark a
/// folder.
/// </summary>
/// <remarks>
/// The empty state is the one somebody sees first and it is not a failure, so it is drawn on the
/// positive surface with the action that resolves it — the same grammar the duplicates destination
/// uses for «no duplicates», which is also a good answer rather than a blank.
/// </remarks>
public sealed class CoursesViewModel : INotifyPropertyChanged
{
    private readonly GetCourses _getCourses;
    private readonly AsyncRelayCommand _open;
    private readonly AsyncRelayCommand _resume;
    private IReadOnlyList<CourseCardViewModel> _cards = [];
    private ICommand? _markFolder;

    public CoursesViewModel(GetCourses getCourses)
    {
        _getCourses = getCourses ?? throw new ArgumentNullException(nameof(getCourses));

        // The one command type this tree has, even for work that finishes at once: a second,
        // synchronous kind would be a second place where a failure could go unhandled, which is the
        // defect ARQ-004 removed.
        _open = new AsyncRelayCommand(
            parameter => Raise(Opened, parameter),
            parameter => parameter is CourseCardViewModel);
        _resume = new AsyncRelayCommand(
            parameter => Raise(Resumed, parameter),
            parameter => parameter is CourseCardViewModel { CanAct: true });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Somebody asked for a course's card. The shell decides what opening means.</summary>
    public event EventHandler<CourseId>? Opened;

    /// <summary>
    /// Somebody asked to carry on with a course. Separate from <see cref="Opened"/> because they are
    /// different offers: one shows the lessons, the other starts playing one.
    /// </summary>
    public event EventHandler<CourseId>? Resumed;

    /// <summary>
    /// Marking a folder, which opens the add-media dialog. The shell wires it, the same way it wires
    /// the duplicates list's opener: the door already belongs to the shell, and a second one built
    /// here would be a second door onto the same room that could drift from it.
    /// </summary>
    public ICommand? MarkFolderCommand
    {
        get => _markFolder;
        set
        {
            _markFolder = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<CourseCardViewModel> Cards
    {
        get => _cards;
        private set
        {
            _cards = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasCourses));
        }
    }

    public bool IsEmpty => Cards.Count == 0;

    public bool HasCourses => Cards.Count > 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var cards = await _getCourses.ListAsync(cancellationToken).ConfigureAwait(true);
        Cards = [.. cards.Select(card => new CourseCardViewModel(card, _open, _resume))];
    }

    private Task Raise(EventHandler<CourseId>? handler, object? parameter)
    {
        // The parameter is not tested again: CanExecute refuses anything that is not a card before
        // this runs, so a second check would be a branch nothing can take.
        handler?.Invoke(this, ((CourseCardViewModel)parameter!).Id);
        return Task.CompletedTask;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// The words and the numbers the course surfaces build rather than pick.
/// </summary>
/// <remarks>
/// The fallbacks are not decoration: a headless test mounts these models without the string
/// dictionaries, and a null there would print a bare number where a sentence belongs.
/// </remarks>
internal static class CourseText
{
    /// <summary>
    /// «M2·L06», which is the prototype's own shorthand for where the thread is. The lesson is
    /// padded to two digits and the module is not, because a course has tens of lessons and a
    /// handful of modules.
    /// </summary>
    public static string Coordinates(CourseThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);
        return string.Create(
            CultureInfo.CurrentCulture,
            $"M{thread.ModuleNumber}·L{thread.LessonNumber:00}");
    }

    /// <summary>
    /// «2 h 10 min», and «45 min» below the hour. Built from the digits and the two unit letters the
    /// prototype uses, which are the same in both languages — a duration is not a sentence.
    /// </summary>
    public static string Duration(TimeSpan value)
    {
        var minutes = (int)Math.Round(value.TotalMinutes, MidpointRounding.AwayFromZero);
        var hours = minutes / 60;
        return hours > 0
            ? string.Create(CultureInfo.CurrentCulture, $"{hours} h {minutes % 60} min")
            : string.Create(CultureInfo.CurrentCulture, $"{minutes} min");
    }

    public static string Resource(string key, string fallback) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            && value is string text
                ? text
                : fallback;
}
