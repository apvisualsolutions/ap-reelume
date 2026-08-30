// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;
using Microsoft.Data.Sqlite;

namespace ApSolutions.LocalMedia.Infrastructure.Data.Repositories;

/// <summary>
/// Courses and their lessons, and the depth a root declares (CRS-001, migration 0022).
/// </summary>
/// <remarks>
/// Saving replaces a course's lessons rather than merging them, because re-reading a folder is the
/// whole answer to what is in it: a lesson that is gone from the disk has to be gone from the list,
/// and merging would leave it there forever. What survives the replacement is progress, which does
/// not live here — it hangs off the file identity of LIB-009 and is untouched by this table.
/// </remarks>
public sealed class CourseRepository : ICourseRepository, ICourseRootDeclarationStore
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public CourseRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, root_id, relative_path, title, marked_at, last_opened_at
            FROM courses
            ORDER BY title COLLATE NOCASE, id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var courses = new List<Course>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            courses.Add(ReadCourse(reader));
        }

        return courses;
    }

    public async Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, root_id, relative_path, title, marked_at, last_opened_at
            FROM courses
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", Text(id.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadCourse(reader) : null;
    }

    public async Task<CourseId> SaveAsync(
        Course course,
        IReadOnlyList<Lesson> lessons,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(lessons);

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var id = await ExistingIdAsync(connection, transaction, course, cancellationToken).ConfigureAwait(false)
            ?? course.Id;

        await using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = (SqliteTransaction)transaction;
            upsert.CommandText = """
                INSERT INTO courses (id, root_id, relative_path, title, marked_at, last_opened_at)
                VALUES ($id, $rootId, $path, $title, $markedAt, $lastOpenedAt)
                ON CONFLICT(root_id, relative_path) DO UPDATE SET title = excluded.title;
                """;
            upsert.Parameters.AddWithValue("$id", Text(id.Value));
            upsert.Parameters.AddWithValue("$rootId", Text(course.RootId.Value));
            upsert.Parameters.AddWithValue("$path", course.RelativePath);
            upsert.Parameters.AddWithValue("$title", course.Title);
            upsert.Parameters.AddWithValue("$markedAt", Timestamp(course.MarkedAtUtc));
            upsert.Parameters.AddWithValue("$lastOpenedAt", (object?)Timestamp(course.LastOpenedAtUtc) ?? DBNull.Value);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var clear = connection.CreateCommand())
        {
            clear.Transaction = (SqliteTransaction)transaction;
            clear.CommandText = "DELETE FROM lessons WHERE course_id = $courseId;";
            clear.Parameters.AddWithValue("$courseId", Text(id.Value));
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var lesson in lessons)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = (SqliteTransaction)transaction;
            insert.CommandText = """
                INSERT INTO lessons (
                    id, course_id, media_file_id, module, module_sort_major, module_sort_minor,
                    sort_major, sort_minor, name, title, relative_path)
                VALUES (
                    $id, $courseId, $mediaFileId, $module, $moduleMajor, $moduleMinor,
                    $major, $minor, $name, $title, $path);
                """;
            insert.Parameters.AddWithValue("$id", Text(lesson.Id.Value));
            insert.Parameters.AddWithValue("$courseId", Text(id.Value));
            insert.Parameters.AddWithValue(
                "$mediaFileId",
                lesson.MediaFileId is { } file ? Text(file.Value) : DBNull.Value);
            insert.Parameters.AddWithValue("$module", (object?)lesson.Module ?? DBNull.Value);
            insert.Parameters.AddWithValue("$moduleMajor", Major(lesson.ModuleOrdinal));
            insert.Parameters.AddWithValue("$moduleMinor", Minor(lesson.ModuleOrdinal));
            insert.Parameters.AddWithValue("$major", Major(lesson.Ordinal));
            insert.Parameters.AddWithValue("$minor", Minor(lesson.Ordinal));
            insert.Parameters.AddWithValue("$name", lesson.Name);
            insert.Parameters.AddWithValue("$title", lesson.Title);
            insert.Parameters.AddWithValue("$path", lesson.RelativePath);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<Lesson>> ListLessonsAsync(
        CourseId courseId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();

        // The order is the one the policy decided when the folder was read, kept rather than
        // recomputed: numbered lessons by their number, then everything unnumbered by name. A NULL
        // sort_major is what "unnumbered" is stored as, so it has to sort last explicitly — SQLite
        // puts NULL first and would open every course with the material that carries no number.
        command.CommandText = """
            SELECT id, course_id, media_file_id, module, module_sort_major, module_sort_minor,
                   sort_major, sort_minor, name, title, relative_path
            FROM lessons
            WHERE course_id = $courseId
            ORDER BY
                module_sort_major IS NULL, module_sort_major, module_sort_minor,
                module COLLATE NOCASE,
                sort_major IS NULL, sort_major, sort_minor,
                name COLLATE NOCASE,
                id;
            """;
        command.Parameters.AddWithValue("$courseId", Text(courseId.Value));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var lessons = new List<Lesson>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lessons.Add(ReadLesson(reader));
        }

        return lessons;
    }

    public async Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM courses WHERE id = $id;";
        command.Parameters.AddWithValue("$id", Text(id.Value));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TouchAsync(
        CourseId id,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE courses SET last_opened_at = $openedAt WHERE id = $id;";
        command.Parameters.AddWithValue("$id", Text(id.Value));
        command.Parameters.AddWithValue("$openedAt", Timestamp(openedAtUtc)!);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int?> GetCourseDepthAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT course_depth FROM library_roots WHERE id = $id;";
        command.Parameters.AddWithValue("$id", Text(rootId.Value));
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public async Task DeclareAsync(
        LibraryRootId rootId,
        int? courseDepth,
        CancellationToken cancellationToken = default)
    {
        if (courseDepth is { } depth)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        }

        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE library_roots SET course_depth = $depth WHERE id = $id;";
        command.Parameters.AddWithValue("$id", Text(rootId.Value));
        command.Parameters.AddWithValue("$depth", (object?)courseDepth ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<CourseId?> ExistingIdAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        Course course,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            SELECT id FROM courses
            WHERE root_id = $rootId AND relative_path = $path COLLATE NOCASE;
            """;
        command.Parameters.AddWithValue("$rootId", Text(course.RootId.Value));
        command.Parameters.AddWithValue("$path", course.RelativePath);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is string stored ? new CourseId(Guid.Parse(stored)) : null;
    }

    private static Course ReadCourse(SqliteDataReader reader) => new(
        new CourseId(Guid.Parse(reader.GetString(0))),
        new LibraryRootId(Guid.Parse(reader.GetString(1))),
        reader.GetString(2),
        reader.GetString(3),
        ReadTimestamp(reader.GetString(4)),
        reader.IsDBNull(5) ? null : ReadTimestamp(reader.GetString(5)));

    private static Lesson ReadLesson(SqliteDataReader reader) => new(
        new LessonId(Guid.Parse(reader.GetString(0))),
        new CourseId(Guid.Parse(reader.GetString(1))),
        reader.IsDBNull(2) ? null : new MediaFileId(Guid.Parse(reader.GetString(2))),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        ReadOrdinal(reader, 4, 5),
        ReadOrdinal(reader, 6, 7),
        reader.GetString(8),
        reader.GetString(9),
        reader.GetString(10));

    private static LessonOrdinal? ReadOrdinal(SqliteDataReader reader, int major, int minor) =>
        reader.IsDBNull(major)
            ? null
            : new LessonOrdinal(reader.GetInt32(major), reader.IsDBNull(minor) ? null : reader.GetInt32(minor));

    private static object Major(LessonOrdinal? ordinal) =>
        ordinal is { } value ? value.Major : DBNull.Value;

    private static object Minor(LessonOrdinal? ordinal) =>
        ordinal?.Minor is { } value ? value : DBNull.Value;

    private static string Text(Guid value) => value.ToString("D");

    private static string? Timestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ReadTimestamp(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
