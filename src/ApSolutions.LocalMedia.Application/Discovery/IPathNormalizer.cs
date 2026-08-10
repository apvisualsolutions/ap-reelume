// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public enum LibraryRootPathError
{
    InvalidPath,
    KindMismatch,
    NotFound,
    AccessDenied,
}

public sealed class LibraryRootPathException : InvalidOperationException
{
    public LibraryRootPathException(
        LibraryRootPathError error,
        string path,
        RootKind kind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Error = error;
        Path = path;
        Kind = kind;
    }

    public LibraryRootPathError Error { get; }

    public string Path { get; }

    public RootKind Kind { get; }
}

public interface IPathNormalizer
{
    string NormalizeAndValidate(string path, RootKind kind);
}
