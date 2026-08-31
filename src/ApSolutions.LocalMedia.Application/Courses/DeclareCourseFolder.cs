// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>
/// One pointed-at folder, as a course (CRS-001, ADR-0006 amendment 1).
/// </summary>
/// <param name="CourseFolderPath">The folder a person pointed at. Not a root: one course.</param>
/// <param name="Kind">
/// Which kind of volume the path sits on, as the dialog detected it. It is only used when a root has
/// to be added, and it is asked for rather than worked out here because reading a drive is the
/// host's job — this layer has no business knowing what a removable volume looks like.
/// </param>
/// <param name="AlsoMark">
/// The neighbours to claim as well, answered after the fact by «Hemos encontrado {0} carpetas más.
/// ¿Son todas cursos?». Empty on the first pass, which is what makes that question honest: nothing
/// but the named folder is marked until somebody says yes.
/// </param>
public sealed record DeclareCourseFolderCommand(
    string CourseFolderPath,
    RootKind Kind = RootKind.Local,
    IReadOnlyCollection<string>? AlsoMark = null);

/// <summary>
/// What the gesture declared, and what it left to ask about.
/// </summary>
/// <param name="Others">
/// The folders found at the same depth and deliberately not marked. The dialog counts these into its
/// question; an empty list is the case where there is nothing to ask.
/// </param>
public sealed record DeclaredCourseFolder(
    LibraryRootId RootId,
    int CourseDepth,
    IReadOnlyList<MarkedCourse> Marked,
    IReadOnlyList<string> Others);

/// <summary>
/// Turning "this folder is a course" into a declared root at a derived depth (CRS-001).
/// </summary>
/// <remarks>
/// This is the door ADR-0006 decision 2 asks for: the signal is the user's and never the program's.
/// What amendment 1 changed is its shape — a folder is pointed at instead of a number being typed —
/// and <see cref="CourseRootDeclarationPolicy"/> holds the derivation so this class only has to
/// arrange the writes.
/// <para>
/// The root it lands on is an existing one whenever the folder is already inside a catalogued root,
/// because a root inside a root is refused, and the folder's parent otherwise. A root added here is
/// <see cref="ScanPolicy.Manual"/> on purpose: the dialog's own help promises the rest of the drive
/// is not scanned, and a policy that scans on startup would quietly break that promise.
/// </para>
/// <para>
/// The neighbours are asked about rather than claimed, and the second pass re-reads the root rather
/// than remembering the first one's answer. It costs one more walk of a folder somebody is standing
/// in front of, and it buys an answer that is true when it is acted on instead of true when it was
/// computed.
/// </para>
/// </remarks>
public sealed class DeclareCourseFolder
{
    private readonly ILibraryRootRepository _roots;
    private readonly AddLibraryRoot _addLibraryRoot;
    private readonly MarkCoursesInRoot _markCourses;

    public DeclareCourseFolder(
        ILibraryRootRepository roots,
        AddLibraryRoot addLibraryRoot,
        MarkCoursesInRoot markCourses)
    {
        _roots = roots ?? throw new ArgumentNullException(nameof(roots));
        _addLibraryRoot = addLibraryRoot ?? throw new ArgumentNullException(nameof(addLibraryRoot));
        _markCourses = markCourses ?? throw new ArgumentNullException(nameof(markCourses));
    }

    /// <summary>
    /// Declares the root the folder implies, marks that folder, and names what else sits beside it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The path is not one a course can be declared from: empty, a catalogued root itself, or a
    /// folder straight on a drive. The dialog answers these on screen rather than throwing on.
    /// </exception>
    public async Task<DeclaredCourseFolder> ExecuteAsync(
        DeclareCourseFolderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _roots.ListAsync(cancellationToken).ConfigureAwait(false);
        var declaration = CourseRootDeclarationPolicy.Derive(
            command.CourseFolderPath,
            existing.Select(root => root.Path))
            ?? throw new ArgumentException(
                $"No course can be declared from: {command.CourseFolderPath}",
                nameof(command));

        var rootId = declaration.IsExistingRoot
            ? existing.First(root => string.Equals(
                root.Path,
                declaration.RootPath,
                StringComparison.OrdinalIgnoreCase)).Id
            : (await _addLibraryRoot.ExecuteAsync(
                new AddLibraryRootCommand(declaration.RootPath, command.Kind, ScanPolicy.Manual),
                cancellationToken).ConfigureAwait(false)).Id;

        // The pointed-at folder always goes in, and the neighbours only once they have been said
        // yes to. Both are named, so an unfiltered pass — which would claim whatever detection
        // found — never happens through this door.
        var wanted = new List<string> { declaration.RelativePath };
        if (command.AlsoMark is { Count: > 0 } also)
        {
            wanted.AddRange(also);
        }

        var marked = await _markCourses.ExecuteAsync(
            new MarkCoursesInRootCommand(rootId, declaration.CourseDepth, OnlyRelativePaths: wanted),
            cancellationToken).ConfigureAwait(false);

        return new DeclaredCourseFolder(
            rootId,
            declaration.CourseDepth,
            marked.Marked,
            marked.Others);
    }
}
