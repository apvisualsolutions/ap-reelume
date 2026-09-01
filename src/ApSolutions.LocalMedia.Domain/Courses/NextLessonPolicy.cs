// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// Which lesson follows which (CRS-004), for the countdown that offers the next one when one ends.
/// </summary>
/// <remarks>
/// The episode chain needs a policy to decide an <i>order</i> — seasons, episode numbers, specials
/// last. This one does not: <see cref="ICourseRepository.ListLessonsAsync"/> hands lessons back in
/// the order the file names decided, and CRS-001 made that order the stored one precisely so nothing
/// downstream would re-derive it and get a different answer. So what is left here is the one
/// question the order does not answer: what comes after this, and can it be played.
/// <para>
/// There is no equivalent of the specials rule. A course has no deliberate-choice section to refuse
/// to run into, so the chain simply stops at the end of the course — which is the state
/// «Curso terminado» is written for.
/// </para>
/// </remarks>
public static class NextLessonPolicy
{
    /// <summary>
    /// The next playable lesson after <paramref name="current"/>, or <see langword="null"/> when
    /// there is none left.
    /// </summary>
    /// <remarks>
    /// A lesson whose file the catalogue has not seen is stepped over rather than offered: the row
    /// on the card refuses to play for exactly the same reason, and a countdown that ran down to a
    /// lesson it cannot open would have spent ten seconds promising something it then failed at.
    /// <para>
    /// A watched lesson is <b>not</b> skipped. The thread's rule — first unwatched in order — is for
    /// answering «where was I» when a course is opened; this is the person sitting in front of the
    /// lesson that just ended, and jumping them past the next one because they saw it once before is
    /// the application deciding what they meant.
    /// </para>
    /// </remarks>
    public static CourseLessonProgress? FindNext(
        IReadOnlyList<CourseLessonProgress> lessons,
        LessonId current)
    {
        ArgumentNullException.ThrowIfNull(lessons);

        var index = -1;
        for (var position = 0; position < lessons.Count; position++)
        {
            if (lessons[position].Id == current)
            {
                index = position;
                break;
            }
        }

        if (index < 0)
        {
            return null;
        }

        for (var position = index + 1; position < lessons.Count; position++)
        {
            if (lessons[position].MediaFileId is not null)
            {
                return lessons[position];
            }
        }

        return null;
    }
}
