// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// Which root a pointed-at course folder belongs to, and how deep it sits in it (ADR-0006
/// decision 3, as amended on 2026-08-31).
/// </summary>
/// <param name="RootPath">
/// The library root the declaration belongs to: an existing one when the folder is already inside
/// it, and the folder's own parent when it is not.
/// </param>
/// <param name="CourseDepth">How many folder levels below <see cref="RootPath"/> the course sits.</param>
/// <param name="RelativePath">
/// The pointed-at folder below <see cref="RootPath"/>, with forward slashes — the same shape
/// <see cref="CourseStructurePolicy"/> hands back, so one can be looked up among the other.
/// </param>
/// <param name="IsExistingRoot">
/// Whether <see cref="RootPath"/> is a root the catalogue already holds. False means one has to be
/// added before anything can be declared on it.
/// </param>
public sealed record CourseRootDeclaration(
    string RootPath,
    int CourseDepth,
    string RelativePath,
    bool IsExistingRoot);

/// <summary>
/// The gesture that declares a course root: a person points at <em>one</em> course folder and the
/// depth is read off that, rather than typed as a number (ADR-0006, amendment 1).
/// </summary>
/// <remarks>
/// Decision 3 is unchanged in what it decides — the program never guesses the depth — and changed
/// only in how it arrives. Guessing was measured and does not work: the candidate rule returned
/// <b>31 courses where there are 12</b> over a real collection. A derived depth is the same number
/// that used to be typed, so every measurement behind decision 3 still stands.
/// <para>
/// Two answers come out of one gesture because they are one question. Pointing at
/// <c>D:\Cursos\3D\Composición</c> when <c>D:\Cursos</c> is already a root means depth 2 in that
/// root; pointing at it when nothing is catalogued means <c>D:\Cursos\3D</c> becomes a root and the
/// depth is 1. Both readings put the siblings of the pointed-at folder at the same level as it,
/// which is what the amendment offers to mark next.
/// </para>
/// </remarks>
public static class CourseRootDeclarationPolicy
{
    private static readonly char[] Separators = ['/', '\\'];

    /// <summary>
    /// Reads the root and the depth off <paramref name="courseFolderPath"/>, or answers
    /// <see langword="null"/> when no declaration can be made from it.
    /// </summary>
    /// <param name="courseFolderPath">The folder a person pointed at, as one course.</param>
    /// <param name="existingRootPaths">The paths of the roots the catalogue holds right now.</param>
    /// <remarks>
    /// Three shapes answer null, and each is something a person can really point at rather than a
    /// defensive branch. An empty path is the dialog's own starting state, which arrives here every
    /// time somebody opens it. A path that <em>is</em> a catalogued root already is a root, not a
    /// course inside one. And a folder sitting directly on a drive would make the whole volume a
    /// root, which is the one thing this dialog's help promises it will not do.
    /// </remarks>
    public static CourseRootDeclaration? Derive(string courseFolderPath, IEnumerable<string> existingRootPaths)
    {
        ArgumentNullException.ThrowIfNull(existingRootPaths);
        if (string.IsNullOrWhiteSpace(courseFolderPath))
        {
            return null;
        }

        var folder = Canonical(courseFolderPath);
        foreach (var rootPath in existingRootPaths)
        {
            var root = Canonical(rootPath);
            if (string.Equals(root, folder, StringComparison.OrdinalIgnoreCase))
            {
                // A root is not a course inside itself: depth 0 is not a depth, and marking it
                // would make every one of its children a course without anybody saying so.
                return null;
            }

            if (Below(root, folder) is { Length: > 0 } relative)
            {
                // The root goes back as the catalogue spells it and not as this comparison spelled
                // it, because the caller looks the root up by that exact string.
                return new CourseRootDeclaration(
                    TrimTrailingSeparator(rootPath.Trim()),
                    relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Length,
                    relative,
                    IsExistingRoot: true);
            }
        }

        // Nothing catalogued holds it, so the folder's own parent becomes the root and the folder
        // sits one level down. That is the reading the amendment asks for: the siblings it offers
        // to mark next are exactly the parent's other folders.
        return ParentOf(folder) is { Length: > 0 } parent
            ? new CourseRootDeclaration(parent, CourseDepth: 1, Below(parent, folder), IsExistingRoot: false)
            : null;
    }

    /// <summary>
    /// The part of <paramref name="path"/> below <paramref name="root"/> with forward slashes, or
    /// the empty string when it is not below it.
    /// </summary>
    private static string Below(string root, string path) =>
        path.StartsWith(root + '\\', StringComparison.OrdinalIgnoreCase)
            ? string.Join('/', path[(root.Length + 1)..].Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            : string.Empty;

    /// <summary>
    /// The folder above <paramref name="path"/>, or the empty string when there is none.
    /// </summary>
    /// <remarks>
    /// Written here rather than taken from <c>Path.GetDirectoryName</c> because this policy is pure:
    /// its answer must not change with the separator the running platform happens to prefer.
    /// </remarks>
    private static string ParentOf(string path)
    {
        var cut = path.LastIndexOfAny(Separators);
        if (cut <= 0)
        {
            return string.Empty;
        }

        // "D:\Curso" cuts to "D:", and a drive is not a folder anything can be added under: making
        // a whole volume a library root is what this dialog's own help promises not to do.
        var parent = path[..cut];
        return parent.EndsWith(':') ? string.Empty : parent;
    }

    /// <summary>
    /// One spelling of a path, for comparing two of them. Windows takes both separators and a person
    /// pastes whichever their source used, so a folder written with <c>/</c> has to find the root the
    /// catalogue wrote with <c>\</c> — comparing them as typed simply never matches.
    /// </summary>
    private static string Canonical(string path) =>
        TrimTrailingSeparator(path.Trim()).Replace('/', '\\');

    private static string TrimTrailingSeparator(string path) =>
        path.Length > 1 && path[^1] is '/' or '\\' ? path[..^1] : path;
}
