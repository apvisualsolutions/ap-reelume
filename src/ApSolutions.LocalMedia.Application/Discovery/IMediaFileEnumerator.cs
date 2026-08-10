// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Discovery;

public sealed record EnumeratedFile(
    string Path,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string? ErrorCode = null);

public interface IMediaFileEnumerator
{
    IAsyncEnumerable<IReadOnlyList<EnumeratedFile>> EnumerateBatchesAsync(
        LibraryRoot root,
        string? afterPath,
        int batchSize,
        CancellationToken cancellationToken = default);
}
