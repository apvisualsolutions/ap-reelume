// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// One video file, as a lesson (CRS-001). <see cref="Name"/> is what the file is called and
/// <see cref="Title"/> is what is left once its leading number is read off, which are two different
/// things: the name is what the order is computed from and the title is what a person reads.
/// </summary>
public sealed record DetectedLesson(string RelativePath, string Name, LessonOrdinal? Ordinal, string Title);

/// <summary>
/// A module of a course. <see cref="Name"/> is <see langword="null"/> for the lessons that sit
/// directly in the course folder, which is a course with no modules at all rather than a course with
/// one module nobody named.
/// </summary>
public sealed record DetectedCourseSection(
    string? Name,
    LessonOrdinal? Ordinal,
    string? Title,
    IReadOnlyList<DetectedLesson> Lessons);

/// <summary>A course folder and everything watchable under it (CRS-001).</summary>
public sealed record DetectedCourse(
    string RelativePath,
    string Name,
    LessonOrdinal? Ordinal,
    string Title,
    IReadOnlyList<DetectedCourseSection> Sections);

/// <summary>
/// Which folders under a course root are courses, and what is inside them (ADR-0006 decision 3).
/// </summary>
/// <remarks>
/// The depth is declared and never guessed. Guessing was tried and measured not to work: the
/// candidate rule — a video leaf, the course as the ancestor at distance 0 or 1, sections recognised
/// by a leading number — returned <b>31 courses where there are 12</b> over a real collection, and
/// its four failure modes are all ordinary. Sections named <c>Lección N</c>, sections numbered at the
/// end rather than the head, technical folders a publisher's player interleaves with no numbering at
/// all, and a video folder four levels below the root inside one of those. Each is fixable with a
/// patch to the pattern, and that is exactly the problem: the rule would be correct until the next
/// course somebody downloads.
/// <para>
/// With the depth declared the answer is exact by construction. Below the course, a subfolder that
/// holds video is a section and anything deeper flattens against it; a resource folder with no video
/// in it is not a section, and that comes free from being fed video paths rather than a directory
/// listing. It matters more than it sounds: of 1955 files measured in one collection only 595 were
/// video, and the other 69 % were image sequences, 3D and compositing scenes, PDFs and archives.
/// </para>
/// </remarks>
public static class CourseStructurePolicy
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// The courses in <paramref name="relativeVideoPaths"/>, which are paths relative to the root
    /// and already filtered to approved video extensions.
    /// </summary>
    /// <param name="courseDepth">
    /// How many folder levels down the root a course sits: 1 for <c>root / course / …</c>, 2 for
    /// <c>root / category / course / …</c>. The two real roots measured have different answers, which
    /// is why no constant serves both.
    /// </param>
    public static IReadOnlyList<DetectedCourse> Detect(IEnumerable<string> relativeVideoPaths, int courseDepth)
    {
        ArgumentNullException.ThrowIfNull(relativeVideoPaths);
        ArgumentOutOfRangeException.ThrowIfLessThan(courseDepth, 1);

        var courses = new Dictionary<string, Builder>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in relativeVideoPaths)
        {
            var segments = (path ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries);

            // A video shallower than the course depth belongs to no course. Claiming it for the
            // nearest folder is the guess this policy exists to refuse.
            if (segments.Length <= courseDepth)
            {
                continue;
            }

            var relativePath = string.Join('/', segments.Take(courseDepth));
            if (!courses.TryGetValue(relativePath, out var course))
            {
                course = new Builder(relativePath, segments[courseDepth - 1]);
                courses.Add(relativePath, course);
            }

            var name = Path.GetFileNameWithoutExtension(segments[^1]);
            course.Add(
                segments.Length > courseDepth + 1 ? segments[courseDepth] : null,
                new DetectedLesson(
                    string.Join('/', segments),
                    name,
                    CourseLessonOrderPolicy.ReadOrdinal(name),
                    CourseLessonOrderPolicy.ReadTitle(name)));
        }

        return CourseLessonOrderPolicy.Order(
            courses.Values.Select(builder => builder.Build()),
            course => course.Name);
    }

    private sealed class Builder(string relativePath, string name)
    {
        private readonly Dictionary<string, List<DetectedLesson>> _sections = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<DetectedLesson> _loose = [];

        public void Add(string? section, DetectedLesson lesson)
        {
            if (section is null)
            {
                _loose.Add(lesson);
                return;
            }

            if (!_sections.TryGetValue(section, out var lessons))
            {
                lessons = [];
                _sections.Add(section, lessons);
            }

            lessons.Add(lesson);
        }

        public DetectedCourse Build()
        {
            var sections = new List<DetectedCourseSection>();

            // The unnamed section comes first because it has no name to sort by, and because the
            // files loose in a course folder are the ones that open it.
            if (_loose.Count > 0)
            {
                sections.Add(new DetectedCourseSection(null, null, null, Ordered(_loose)));
            }

            sections.AddRange(CourseLessonOrderPolicy
                .Order(_sections, entry => entry.Key)
                .Select(entry => new DetectedCourseSection(
                    entry.Key,
                    CourseLessonOrderPolicy.ReadOrdinal(entry.Key),
                    CourseLessonOrderPolicy.ReadTitle(entry.Key),
                    Ordered(entry.Value))));

            return new DetectedCourse(
                relativePath,
                name,
                CourseLessonOrderPolicy.ReadOrdinal(name),
                CourseLessonOrderPolicy.ReadTitle(name),
                sections);
        }

        private static IReadOnlyList<DetectedLesson> Ordered(IEnumerable<DetectedLesson> lessons) =>
            CourseLessonOrderPolicy.Order(lessons, lesson => lesson.Name);
    }
}
