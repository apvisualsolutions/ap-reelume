// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public sealed class WindowsPathNormalizer : IPathNormalizer
{
    private readonly Func<string, bool> _directoryExists;
    private readonly Action<string> _assertReadable;

    public WindowsPathNormalizer()
        : this(Directory.Exists, AssertDirectoryReadable)
    {
    }

    public WindowsPathNormalizer(
        Func<string, bool> directoryExists,
        Action<string> assertReadable)
    {
        _directoryExists = directoryExists ?? throw new ArgumentNullException(nameof(directoryExists));
        _assertReadable = assertReadable ?? throw new ArgumentNullException(nameof(assertReadable));
    }

    public string NormalizeAndValidate(string path, RootKind kind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw CreateError(LibraryRootPathError.InvalidPath, path ?? string.Empty, kind);
        }

        var windowsPath = path.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var isUnc = windowsPath.StartsWith(@"\\", StringComparison.Ordinal);
        if ((kind == RootKind.Unc) != isUnc)
        {
            throw CreateError(LibraryRootPathError.KindMismatch, windowsPath, kind);
        }

        string normalized;
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(windowsPath));
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            throw CreateError(LibraryRootPathError.InvalidPath, windowsPath, kind, exception);
        }

        if (!_directoryExists(normalized))
        {
            throw CreateError(LibraryRootPathError.NotFound, normalized, kind);
        }

        try
        {
            _assertReadable(normalized);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw CreateError(LibraryRootPathError.AccessDenied, normalized, kind, exception);
        }

        return normalized;
    }

    private static void AssertDirectoryReadable(string path)
    {
        using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        _ = enumerator.MoveNext();
    }

    private static LibraryRootPathException CreateError(
        LibraryRootPathError error,
        string path,
        RootKind kind,
        Exception? innerException = null) =>
        new(error, path, kind, $"{error}: cannot use {kind} library root '{path}'.", innerException);
}
