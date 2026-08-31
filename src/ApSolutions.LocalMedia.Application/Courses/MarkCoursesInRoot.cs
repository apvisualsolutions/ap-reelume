// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>
/// Declares a root to hold courses at <paramref name="CourseDepth"/> and reads it (CRS-001).
/// </summary>
/// <param name="CourseDepth">
/// How many folder levels down a course sits. It is the user's answer and never the program's:
/// see <see cref="CourseStructurePolicy"/> for the measurement that rejected guessing it.
/// </param>
/// <param name="OnlyRelativePaths">
/// Which of the detected folders to actually mark, or <see langword="null"/> for all of them.
/// Pointing at one course declares the depth for the whole root, and at that depth there may be
/// neighbours the person has not said anything about yet — ADR-0006 amendment 1 has the application
/// ask before claiming them, so the first pass names the one folder and the answer decides the rest.
/// </param>
public sealed record MarkCoursesInRootCommand(
    LibraryRootId RootId,
    int CourseDepth,
    int BatchSize = 500,
    IReadOnlyCollection<string>? OnlyRelativePaths = null);

public sealed record MarkedCourse(CourseId Id, string RelativePath, string Title, int ModuleCount, int LessonCount);

/// <summary>
/// What one pass marked, and what it found at the same depth and left alone.
/// </summary>
/// <param name="Others">
/// The detected folders <see cref="MarkCoursesInRootCommand.OnlyRelativePaths"/> excluded — the
/// neighbours the dialog counts into «Hemos encontrado {0} carpetas más». Empty when nothing was
/// excluded, which is also what an unfiltered pass answers.
/// </param>
public sealed record MarkedCourses(IReadOnlyList<MarkedCourse> Marked, IReadOnlyList<string> Others)
{
    public static readonly MarkedCourses None = new([], []);
}

/// <summary>
/// Marking a folder of numbered videos as a course, and re-reading one that already is (CRS-001).
/// </summary>
/// <remarks>
/// Nothing here reaches the network, and that is the point rather than an omission: a course root is
/// never identified against a provider, so there is no candidate to weigh, nothing for the review
/// inbox, and no title to send anywhere. The title comes off the folder name and stays local.
/// <para>
/// Nothing is copied, moved or renamed either. The only writes are the root's declared depth and the
/// two course tables; the videos are read and left exactly where they are.
/// </para>
/// <para>
/// It reuses <see cref="IMediaFileEnumerator"/> rather than opening a second way to walk a folder.
/// One enumerator means one set of error codes, one batch budget, and one place where the file
/// system is touched at all.
/// </para>
/// </remarks>
public sealed class MarkCoursesInRoot
{
    private readonly ILibraryRootRepository _roots;
    private readonly ICourseRepository _courses;
    private readonly ICourseRootDeclarationStore _declarations;
    private readonly IMediaFileEnumerator _enumerator;
    private readonly IMediaFileRepository _mediaFiles;
    private readonly IClock _clock;

    public MarkCoursesInRoot(
        ILibraryRootRepository roots,
        ICourseRepository courses,
        ICourseRootDeclarationStore declarations,
        IMediaFileEnumerator enumerator,
        IMediaFileRepository mediaFiles,
        IClock clock)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _courses = courses ?? throw new ArgumentNullException(nameof(courses));
        _declarations = declarations ?? throw new ArgumentNullException(nameof(declarations));
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _mediaFiles = mediaFiles ?? throw new ArgumentNullException(nameof(mediaFiles));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<MarkedCourses> ExecuteAsync(
        MarkCoursesInRootCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfLessThan(command.CourseDepth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(command.BatchSize, 1);

        var root = await _roots.GetAsync(command.RootId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Unknown library root: {command.RootId.Value:D}");

        await _declarations
            .DeclareAsync(root.Id, command.CourseDepth, cancellationToken)
            .ConfigureAwait(false);

        var absoluteByRelative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await foreach (var batch in _enumerator
            .EnumerateBatchesAsync(root, afterPath: null, command.BatchSize, cancellationToken)
            .ConfigureAwait(false))
        {
            foreach (var file in batch)
            {
                // An entry that carries an error code is an entry the enumerator could not read, and
                // a course is not the place to surface that: the scan already reports it.
                if (file.ErrorCode is not null || !MediaFileExtensions.IsApproved(Path.GetExtension(file.Path)))
                {
                    continue;
                }

                if (Relative(root.Path, file.Path) is { Length: > 0 } relative)
                {
                    absoluteByRelative[relative] = file.Path;
                }
            }
        }

        var detected = CourseStructurePolicy.Detect(absoluteByRelative.Keys, command.CourseDepth);
        if (detected.Count == 0)
        {
            return MarkedCourses.None;
        }

        // The filter is compared against what detection found rather than trusted as given: a path
        // nobody detected is not a course this pass can mark, and one it did detect is claimed by
        // the person who named it. Without the set, every detected folder is marked, which is what
        // an answer of "yes, they are all courses" comes back as.
        var wanted = command.OnlyRelativePaths is null
            ? null
            : new HashSet<string>(command.OnlyRelativePaths, StringComparer.OrdinalIgnoreCase);
        var others = new List<string>();

        var identities = await _mediaFiles
            .FindByPathsAsync(root.Id, absoluteByRelative.Values.ToArray(), cancellationToken)
            .ConfigureAwait(false);

        var markedAt = _clock.UtcNow;
        var marked = new List<MarkedCourse>(detected.Count);
        foreach (var course in detected)
        {
            if (wanted is not null && !wanted.Contains(course.RelativePath))
            {
                others.Add(course.RelativePath);
                continue;
            }

            var lessons = new List<Lesson>();
            foreach (var section in course.Sections)
            {
                foreach (var lesson in section.Lessons)
                {
                    var absolute = absoluteByRelative[lesson.RelativePath];
                    // CourseId is left unset on purpose: SaveAsync owns it, because the course
                    // this belongs to may already exist under another identifier and the upsert is
                    // what discovers which. Writing a guess here would be a second answer.
                    lessons.Add(new Lesson(
                        new LessonId(Guid.NewGuid()),
                        CourseId: default,
                        identities.TryGetValue(absolute, out var media) ? media.Id : null,
                        // The module's title and not its folder name: the number is already in
                        // ModuleOrdinal, and "Módulo {0} · {1}" wants them apart. The raw folder
                        // name is not lost — RelativePath still carries it.
                        section.Title,
                        section.Ordinal,
                        lesson.Ordinal,
                        lesson.Name,
                        lesson.Title,
                        lesson.RelativePath));
                }
            }

            var id = await _courses.SaveAsync(
                new Course(
                    new CourseId(Guid.NewGuid()),
                    root.Id,
                    course.RelativePath,
                    course.Title,
                    markedAt,
                    LastOpenedAtUtc: null),
                lessons,
                cancellationToken).ConfigureAwait(false);

            marked.Add(new MarkedCourse(
                id,
                course.RelativePath,
                course.Title,
                course.Sections.Count(section => section.Name is not null),
                lessons.Count));
        }

        return new MarkedCourses(marked, others);
    }

    /// <summary>
    /// The path of <paramref name="absolute"/> below <paramref name="root"/>, with forward slashes,
    /// or an empty string when it is not below it at all.
    /// </summary>
    /// <remarks>
    /// A path the enumerator hands back from outside the root is dropped rather than trusted: it
    /// would put a lesson of one library into a course of another.
    /// <para>
    /// The separators are normalised here and not left as they came, because
    /// <see cref="CourseStructurePolicy"/> normalises them too, and the lesson it hands back is
    /// looked up in this dictionary by exactly that key. Keying it any other way builds a map whose
    /// keys the very next line cannot find — which is what the tests caught.
    /// </para>
    /// </remarks>
    private static string Relative(string root, string absolute)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? string.Join('/', absolute[prefix.Length..].Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
            : string.Empty;
    }
}
