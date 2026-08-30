// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// Where a lesson's progress lives (CRS-002, CRS-005).
/// </summary>
/// <remarks>
/// It lives in PLY-008's store and nowhere else. A course is to its lessons what a show is to its
/// episodes, so the key takes the shape the store already has — course in the title position, lesson
/// in the episode position — and every piece of continuity that reads a
/// <see cref="ContentKey"/> keeps working without knowing a course exists: resume, the watched
/// threshold of PLY-009, the manual override that wins over it, and the countdown of PLY-011.
/// <para>
/// This is a translation and not a second store, which is the whole of decision 6: inventing a
/// <c>lesson_progress</c> table would have been a second answer to «how far in was I», and two
/// answers to one question is how they start disagreeing.
/// </para>
/// <para>
/// A course identifier cannot collide with a title's. Both are version-4 GUIDs from the same
/// generator, and <c>watch_state.title_id</c> carries no foreign key precisely because what it
/// points at is whatever the key says it is.
/// </para>
/// </remarks>
public static class CourseProgressKey
{
    /// <summary>The key one lesson's progress is stored under.</summary>
    public static ContentKey For(CourseId course, LessonId lesson) =>
        ContentKey.ForEpisode(new TitleId(course.Value), new EpisodeId(lesson.Value));

    /// <summary>
    /// The lesson a key stands for, or <see langword="null"/> when it is not a lesson's key at all —
    /// a film's key has no episode part, and reading one as a lesson would invent a lesson.
    /// </summary>
    public static (CourseId Course, LessonId Lesson)? Read(ContentKey key) =>
        key.EpisodeId is { } lesson
            ? (new CourseId(key.TitleId.Value), new LessonId(lesson.Value))
            : null;
}
