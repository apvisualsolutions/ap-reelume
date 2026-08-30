// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// A course's lessons with their length and their progress joined on (CRS-002, CRS-003).
/// </summary>
/// <remarks>
/// Three tables answer one question, and they are joined here rather than read three times: the
/// lesson, the media file that gives it a length, and the watch state that says how far in somebody
/// got. Both joins are LEFT, and neither absence is a failure — a lesson whose file the catalogue
/// has not seen has no length yet, and a lesson nobody has opened has no state, which is exactly
/// what «not started» means.
/// <para>
/// The watch state is found by the key PLY-008 already stores it under:
/// <see cref="CourseProgressKey"/> puts the course where a title goes and the lesson where an
/// episode goes, so this reads the same rows resume and the countdown do.
/// </para>
/// </remarks>
public sealed class CourseLessonReader : ICourseLessonReader
{
    /// <summary>
    /// The module number a lesson with no module gets. Zero rather than one, so the loose lessons of
    /// a course keep the place the ordering already gives them: ahead of «Módulo 1».
    /// </summary>
    private const int LooseModuleNumber = 0;

    private const string Columns = """
        SELECT l.course_id, l.id, l.module, l.module_sort_major, l.title,
               f.duration_ticks, w.position_ticks, w.status
        FROM lessons AS l
        LEFT JOIN media_files AS f ON f.id = l.media_file_id
        LEFT JOIN watch_state AS w
               ON w.content_key = 'title:' || l.course_id || '/episode:' || l.id
        """;

    private const string Order = """
        ORDER BY l.course_id,
                 l.module_sort_major IS NULL, l.module_sort_major, l.module_sort_minor,
                 l.module COLLATE NOCASE,
                 l.sort_major IS NULL, l.sort_major, l.sort_minor,
                 l.name COLLATE NOCASE,
                 l.id
        """;

    private readonly SqliteConnectionFactory _connectionFactory;

    public CourseLessonReader(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<CourseLessonProgress>> ReadAsync(
        CourseId courseId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{Columns}\nWHERE l.course_id = $courseId\n{Order};";
        command.Parameters.AddWithValue("$courseId", courseId.Value.ToString("D"));
        var lessons = new List<CourseLessonProgress>();
        await ReadInto(command, (_, lesson) => lessons.Add(lesson), cancellationToken).ConfigureAwait(false);
        return lessons;
    }

    public async Task<IReadOnlyDictionary<CourseId, IReadOnlyList<CourseLessonProgress>>> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{Columns}\n{Order};";
        var byCourse = new Dictionary<CourseId, List<CourseLessonProgress>>();
        await ReadInto(
            command,
            (courseId, lesson) =>
            {
                if (!byCourse.TryGetValue(courseId, out var lessons))
                {
                    lessons = [];
                    byCourse.Add(courseId, lessons);
                }

                lessons.Add(lesson);
            },
            cancellationToken).ConfigureAwait(false);

        return byCourse.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<CourseLessonProgress>)entry.Value);
    }

    private static async Task ReadInto(
        SqliteCommand command,
        Action<CourseId, CourseLessonProgress> add,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var numbers = new Dictionary<CourseId, int>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var courseId = new CourseId(Guid.Parse(reader.GetString(0)));

            // The lesson's number on screen counts through the whole course and not through its
            // module: the prototype writes «L06» beside a lesson in module 2, and a number that
            // restarted per module would name two lessons the same.
            numbers.TryGetValue(courseId, out var number);
            numbers[courseId] = ++number;

            add(courseId, new CourseLessonProgress(
                new LessonId(Guid.Parse(reader.GetString(1))),
                reader.IsDBNull(3) ? LooseModuleNumber : reader.GetInt32(3),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                number,
                reader.GetString(4),
                reader.IsDBNull(5) ? TimeSpan.Zero : TimeSpan.FromTicks(reader.GetInt64(5)),
                reader.IsDBNull(6) ? TimeSpan.Zero : TimeSpan.FromTicks(reader.GetInt64(6)),
                reader.IsDBNull(7) ? WatchStatus.NotStarted : (WatchStatus)reader.GetInt32(7)));
        }
    }
}
