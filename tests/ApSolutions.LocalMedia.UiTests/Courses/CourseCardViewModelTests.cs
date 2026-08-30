// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Presentation.Courses;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Courses;

/// <summary>
/// The four offers a course card can make (CRS-003), asserted on the card itself rather than through
/// the shell.
/// </summary>
/// <remarks>
/// Four of them are <c>AvaloniaFact</c>s and the fifth is not, and the split is measured rather than
/// stylistic: <c>CourseText.Resource</c> reads <c>Application.ActualThemeVariant</c>, which verifies
/// the UI thread, so anything reading <c>Meta</c> or <c>ActionText</c> must run on it once an
/// application is alive. As a plain <c>Fact</c> they pass under <c>--filter</c>, where no
/// application exists, and throw with the whole suite, where one does. The guard test touches no
/// text and stays a plain fact. Its four states were reachable only through the
/// autonomous walk until now, and the walk measures behaviour rather than branches — which is why
/// the file sat at 58 % of them with nothing named as missing.
/// <para>
/// And not one assertion here reads a literal string, which cost a red to learn: <c>CourseText</c>
/// looks the word up in the running application's dictionaries and falls back to an English literal
/// when there is none. Run under <c>--filter</c> there is no application and the fallback appears;
/// run with the whole suite there is one, and the localized word does. A test that pins the literal
/// passes alone and fails in company. What is pinned instead is what does not move: the numbers, the
/// separator, and whether two states differ.
/// </para>
/// </remarks>
public sealed class CourseCardViewModelTests
{
    [AvaloniaFact]
    public void A_folder_still_being_walked_offers_nothing_and_fills_no_bar()
    {
        var card = Card(new CourseSummary(0, 0, TimeSpan.Zero), CourseThread.Finished);

        Assert.False(card.CanAct);
        Assert.False(card.IsFinished);
        Assert.Equal(0, card.Progress);
        Assert.NotEqual(string.Empty, card.Meta);
        Assert.NotEqual(string.Empty, card.ActionText);
    }

    /// <summary>
    /// A finished course opens rather than resumes, and its meta drops the «left» half: there is
    /// nothing left, and saying «0 min left» would be true and useless.
    /// </summary>
    [AvaloniaFact]
    public void A_finished_course_opens_and_says_only_how_many_lessons_it_had()
    {
        var card = Card(new CourseSummary(12, 12, TimeSpan.Zero), CourseThread.Finished);

        Assert.True(card.CanAct);
        Assert.True(card.IsFinished);
        Assert.Equal(1, card.Progress);
        Assert.Contains("12", card.Meta, StringComparison.Ordinal);
        Assert.DoesNotContain("·", card.Meta, StringComparison.Ordinal);
    }

    /// <summary>
    /// Picking up and continuing are different words, and the prototype separates them by whether
    /// the lesson the thread points at was already started.
    /// </summary>
    [AvaloniaFact]
    public void A_started_lesson_is_picked_up_and_an_untouched_one_is_continued()
    {
        var partial = Card(
            new CourseSummary(3, 12, TimeSpan.FromMinutes(130)),
            Thread(isPartial: true));
        var fresh = Card(
            new CourseSummary(3, 12, TimeSpan.FromMinutes(130)),
            Thread(isPartial: false));

        // Both arms are taken, and what they choose is a RESOURCE KEY. Without a running
        // application there are no dictionaries, so both fall back to the same literal and the
        // difference in wording is not assertable here -- the shell's own tests cover that. What is
        // assertable, and what this is for, is that neither arm is an offer to a finished course
        // and that both name the lesson the thread points at.
        Assert.Contains("4", partial.ActionText, StringComparison.Ordinal);
        Assert.Contains("4", fresh.ActionText, StringComparison.Ordinal);
        Assert.False(partial.IsFinished);
        Assert.False(fresh.IsFinished);
        Assert.Equal(0.25, partial.Progress);
    }

    /// <summary>
    /// The remaining time crosses the hour, which is the arm the format only takes on a long course.
    /// </summary>
    [AvaloniaFact]
    public void The_meta_counts_the_lessons_and_what_is_left_of_them()
    {
        var hours = Card(new CourseSummary(3, 12, TimeSpan.FromMinutes(130)), Thread(false));
        var minutes = Card(new CourseSummary(3, 12, TimeSpan.FromMinutes(40)), Thread(false));

        Assert.Contains(" · ", hours.Meta, StringComparison.Ordinal);
        Assert.Contains("3", hours.Meta, StringComparison.Ordinal);
        Assert.NotEqual(hours.Meta, minutes.Meta);
        Assert.Contains(hours.Title, hours.AccessibleName, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_needs_its_course_and_both_of_its_commands()
    {
        var card = new CourseCard(
            new CourseId(Guid.NewGuid()), "Título", "ruta",
            new CourseSummary(0, 0, TimeSpan.Zero), CourseThread.Finished, null);

        Assert.Throws<ArgumentNullException>(() => new CourseCardViewModel(null!, Noop, Noop));
        Assert.Throws<ArgumentNullException>(() => new CourseCardViewModel(card, null!, Noop));
        Assert.Throws<ArgumentNullException>(() => new CourseCardViewModel(card, Noop, null!));
    }

    private static CourseThread Thread(bool isPartial) => new(
        new LessonId(Guid.NewGuid()), 2, 4, "Una lección",
        isPartial ? TimeSpan.FromMinutes(3) : TimeSpan.Zero,
        TimeSpan.FromMinutes(10),
        isPartial);

    private static CourseCardViewModel Card(CourseSummary summary, CourseThread thread) => new(
        new CourseCard(
            new CourseId(Guid.NewGuid()),
            "Un curso",
            @"D:\Cursos\Un curso",
            summary,
            thread,
            null),
        Noop,
        Noop);

    private static ICommand Noop { get; } = new NoopCommand();

    private sealed class NoopCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
