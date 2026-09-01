// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.TestSupport;

/// <summary>
/// The ports a course use case needs, stood in for once instead of once per test class.
/// </summary>
/// <remarks>
/// They live here because <see cref="ApSolutions.LocalMedia.Application.Courses.DeclareCourseFolder"/>
/// drives <see cref="ApSolutions.LocalMedia.Application.Courses.MarkCoursesInRoot"/> and therefore
/// needs the same six — and because the dialog's view model drives the same chain from another
/// suite. <c>IMediaFileRepository</c> alone is eighteen members, and a second hand-written copy of
/// it is a second thing to keep in step with the interface.
/// <para>
/// Compiled into each suite by <c>&lt;Compile Include&gt;</c> rather than shared as an assembly,
/// which is what the other four files in this folder already do.
/// </para>
/// </remarks>
internal sealed class StubDeclarations : ICourseRootDeclarationStore
{
    public int? Depth { get; private set; }

    public Task DeclareAsync(
        LibraryRootId rootId,
        int? courseDepth,
        CancellationToken cancellationToken = default)
    {
        Depth = courseDepth;
        return Task.CompletedTask;
    }

    public Task<int?> GetCourseDepthAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default) => Task.FromResult(Depth);
}

/// <summary>Keeps what was saved, and upserts on the folder the way the real store does.</summary>
internal sealed class StubCourses : ICourseRepository
{
    private readonly Dictionary<string, CourseId> _idsByPath = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<CourseId, IReadOnlyList<Lesson>> Saved { get; } = [];

    public Task<CourseId> SaveAsync(
        Course course,
        IReadOnlyList<Lesson> lessons,
        CancellationToken cancellationToken = default)
    {
        if (!_idsByPath.TryGetValue(course.RelativePath, out var id))
        {
            id = course.Id;
            _idsByPath.Add(course.RelativePath, id);
        }

        Saved[id] = lessons;
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<Course>> ListAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Course?> GetAsync(CourseId id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<Lesson>> ListLessonsAsync(
        CourseId courseId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Lesson?> FindLessonByFileAsync(
        MediaFileId fileId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task RemoveAsync(CourseId id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task TouchAsync(
        CourseId id,
        DateTimeOffset openedAtUtc,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>Hands the files back one batch at a time, the way the real enumerator does.</summary>
internal sealed class StubEnumerator(string[] files) : IMediaFileEnumerator
{
    public List<string> Failing { get; } = [];

    public async IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
        LibraryRoot root,
        string? afterPath,
        int batchSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return [.. files.Select(path => new EnumeratedFile(path, 1, default))];
        yield return [.. Failing.Select(path => new EnumeratedFile(path, 0, default, "IoError"))];
    }
}

internal sealed class StubMediaFiles : IMediaFileRepository
{
    private readonly Dictionary<string, MediaFile> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<MediaFileId, MediaFile> _byId = [];

    /// <summary>
    /// A file the catalogue has already seen. The root is not part of the answer:
    /// <see cref="FindByPathsAsync"/> keys on the path alone, exactly as the caller looks it up.
    /// </summary>
    public MediaFileId Add(string path)
    {
        var id = new MediaFileId(Guid.NewGuid());
        var file = new MediaFile(
            id,
            default,
            path,
            1,
            default,
            new TechnicalMetadata(null, string.Empty, [], [], null, null));
        _byPath[path] = file;
        _byId[id] = file;
        return id;
    }

    /// <summary>
    /// The drive pulled out. It is what the lesson chain's revalidation at zero has to find, and the
    /// reason that revalidation is a re-read rather than a recheck of what was already held.
    /// </summary>
    public void Forget(MediaFileId id)
    {
        if (_byId.Remove(id, out var file))
        {
            _byPath.Remove(file.Path);
        }
    }

    public Task<IReadOnlyDictionary<string, MediaFile>> FindByPathsAsync(
        LibraryRootId rootId,
        IReadOnlyCollection<string> paths,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, MediaFile> found = paths
            .Where(_byPath.ContainsKey)
            .ToDictionary(path => path, path => _byPath[path], StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(found);
    }

    public Task<MediaFile?> FindByPathAsync(
        LibraryRootId rootId,
        string path,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<MediaFile?> FindByIdAsync(MediaFileId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_byId.TryGetValue(id, out var file) ? file : null);

    public Task UpsertAsync(MediaFile mediaFile, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpsertBatchAsync(
        IReadOnlyCollection<MediaFile> mediaFiles,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IdentifiedMediaFile?> FindByStableIdentityAsync(
        FileIdentity identity,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<IdentifiedMediaFile>> FindByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SaveIdentityAsync(
        MediaFileId mediaFileId,
        FileIdentity identity,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<FileIdentity?> GetIdentityAsync(
        MediaFileId mediaFileId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task RemoveAsync(MediaFileId mediaFileId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task ReassignAsync(
        MediaFileId mediaFileId,
        LibraryRootId libraryRootId,
        string newPath,
        FileIdentity identity,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SetRootAvailabilityAsync(
        LibraryRootId libraryRootId,
        bool isAvailable,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<string?> GetScanCheckpointAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SaveScanCheckpointAsync(
        LibraryRootId rootId,
        string resumeAfterPath,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task ClearScanCheckpointAsync(
        LibraryRootId rootId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SetScannedTitleAsync(
        MediaFileId mediaFileId,
        ScannedTitle title,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow => now;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>
/// A catalogue of roots that really adds and really lists, because declaring a course reads back
/// the root it has just written.
/// </summary>
internal sealed class CatalogueOfRoots : ILibraryRootRepository
{
    public List<LibraryRoot> All { get; } = [];

    public void Add(string path) => All.Add(new LibraryRoot(
        new LibraryRootId(Guid.NewGuid()),
        path,
        RootKind.Local,
        RootAvailability.Available,
        ScanPolicy.Manual));

    public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default)
    {
        All.Add(root);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<LibraryRoot>>([.. All]);

    public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(All.FirstOrDefault(root => root.Id == id));

    public Task RemoveAsync(
        LibraryRootId id,
        bool preserveCatalog = true,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>
/// The path as written. Normalising is <c>WindowsPathNormalizer</c>'s job and it has its own tests;
/// borrowing it here would make these depend on the running machine's file system.
/// </summary>
internal sealed class VerbatimPathNormalizer : IPathNormalizer
{
    public string NormalizeAndValidate(string path, RootKind kind) => path;
}

/// <summary>Everything a declared course folder needs, wired the way the composition wires it.</summary>
internal sealed class CourseWorld
{
    public CourseWorld(params string[] files)
    {
        Enumerator = new StubEnumerator(files);
        MarkCourses = new MarkCoursesInRoot(
            Roots,
            Courses,
            Declarations,
            Enumerator,
            MediaFiles,
            new FixedClock(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero)));
        Declare = new DeclareCourseFolder(
            Roots,
            new AddLibraryRoot(Roots, new VerbatimPathNormalizer()),
            MarkCourses);
    }

    public CatalogueOfRoots Roots { get; } = new();

    public StubCourses Courses { get; } = new();

    public StubDeclarations Declarations { get; } = new();

    public StubEnumerator Enumerator { get; }

    public StubMediaFiles MediaFiles { get; } = new();

    public MarkCoursesInRoot MarkCourses { get; }

    public DeclareCourseFolder Declare { get; }
}
