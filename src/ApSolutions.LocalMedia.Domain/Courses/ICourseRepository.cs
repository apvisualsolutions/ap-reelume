// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Courses;

public readonly record struct CourseId(Guid Value);

public readonly record struct LessonId(Guid Value);

/// <summary>
/// A course as it is stored: the folder somebody marked, under the root that declares it (CRS-001).
/// </summary>
/// <remarks>
/// <see cref="RelativePath"/> is relative to the root and not absolute, which is what lets a whole
/// library move drive letters without every course losing itself. <see cref="Title"/> starts as the
/// folder's name with its leading number read off and is editable afterwards with the protected
/// editor that already exists (LIB-011).
/// </remarks>
public sealed record Course(
    CourseId Id,
    LibraryRootId RootId,
    string RelativePath,
    string Title,
    DateTimeOffset MarkedAtUtc,
    DateTimeOffset? LastOpenedAtUtc);

/// <summary>
/// One lesson of a course (CRS-005).
/// </summary>
/// <remarks>
/// <see cref="MediaFileId"/> is LIB-009's identity, which is why moving or renaming the file keeps
/// the progress: the lesson is anchored to what the file <i>is</i> and not to where it sits. It is
/// nullable because a lesson whose file has gone is a lesson that is missing, and a surface has to
/// be able to say so — dropping the row would turn it into a lesson that never existed.
/// <para>
/// There is no progress on this record and there will not be one. Progress is PLY-008's store and
/// the watched threshold is PLY-009's; a second store would be a second answer to one question.
/// </para>
/// <para>
/// <see cref="Module"/> is the module's <i>title</i> with its leading number read off, beside
/// <see cref="ModuleOrdinal"/> — the same split as <see cref="Name"/> and <see cref="Title"/>,
/// because «Módulo {0} · {1}» needs the two apart. Nothing is lost by it: the folder's real name is
/// still inside <see cref="RelativePath"/>.
/// </para>
/// </remarks>
public sealed record Lesson(
    LessonId Id,
    CourseId CourseId,
    MediaFileId? MediaFileId,
    string? Module,
    LessonOrdinal? ModuleOrdinal,
    LessonOrdinal? Ordinal,
    string Name,
    string Title,
    string RelativePath);

public interface ICourseRepository
{
    Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default);

    Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the course and replaces its lessons with <paramref name="lessons"/>.
    /// </summary>
    /// <remarks>
    /// Marking a folder that is already a course is the same course re-read, not a second one, so
    /// this upserts on the root and relative path the unique index already enforces. It returns the
    /// identifier the course ended up with, which is the existing one when there was one.
    /// <para>
    /// <see cref="Lesson.CourseId"/> is <b>ignored on the way in</b> and authoritative on the way
    /// out: the caller cannot know which identifier the upsert will settle on, so the store fills it
    /// rather than trusting a guess it would have to correct.
    /// </para>
    /// </remarks>
    Task<CourseId> SaveAsync(
        Course course,
        IReadOnlyList<Lesson> lessons,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Lesson>> ListLessonsAsync(
        CourseId courseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The lesson a media file backs, or <see langword="null"/> when the file is not one.
    /// </summary>
    /// <remarks>
    /// The mirror of <c>IEpisodeSequenceRepository.FindByFileAsync</c>, and it is asked the same way
    /// for the same reason: a playing session holds a file and nothing else. The player's «Lecciones»
    /// panel is <b>absent</b> outside a lesson session (CRS-004), so something has to answer whether
    /// this file is one — and the answer cannot travel on the request that opened it. The countdown
    /// opens the next lesson with nothing but a file id, and so does picking the thread up from the
    /// home rail; a course that rode along on the request would go missing down every path that
    /// forgot to forward it, leaving the panel quietly absent rather than wrong.
    /// <para>
    /// <c>ix_lessons_media_file</c> has existed since migration 0022 with nothing querying it. This
    /// is the query it was created for.
    /// </para>
    /// </remarks>
    Task<Lesson?> FindLessonByFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unmarks a folder. The videos are not touched and nothing on disk changes: what leaves is the
    /// course row and its lessons.
    /// </summary>
    Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default);

    Task TouchAsync(CourseId id, DateTimeOffset openedAtUtc, CancellationToken cancellationToken = default);
}

/// <summary>
/// Which roots declare they hold courses, and at what depth (ADR-0006 decisions 2 and 3).
/// </summary>
/// <remarks>
/// One nullable column carries both answers: a root holds courses exactly when it has a depth. A
/// flag beside a depth could disagree with itself, and a root that claims courses without saying
/// where they are is a root the detection would have to guess about.
/// </remarks>
public interface ICourseRootDeclarationStore
{
    Task<int?> GetCourseDepthAsync(LibraryRootId rootId, CancellationToken cancellationToken = default);

    Task DeclareAsync(LibraryRootId rootId, int? courseDepth, CancellationToken cancellationToken = default);
}
