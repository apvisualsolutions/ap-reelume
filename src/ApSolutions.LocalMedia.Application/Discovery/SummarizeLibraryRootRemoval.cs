// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Application.Discovery;

/// <summary>
/// What removing a folder would cost, so the question can be asked with the numbers in it.
/// </summary>
/// <remarks>
/// <para><b>Titles</b> are the cards that would disappear from the library, counted the way the
/// catalogue projects them and not as a row count: a show is one card however many episodes it has,
/// and an unidentified file is a card only when it is neither a title nor an episode.</para>
///
/// <para><b>Marks</b> are what a person put there — favourite, watch later, rating, and the ranges
/// they kept. What a detector merely proposed is not counted: it comes back by analysing the same
/// file again, so counting it would inflate the number with something nobody chose.</para>
///
/// <para><b>Progress</b> is the watched position that goes, not the running time of the titles. The
/// films stay on the disk; what is lost is the place they were left at.</para>
/// </remarks>
public sealed record LibraryRootRemovalSummary(int TitleCount, int MarkCount, TimeSpan Progress)
{
    /// <summary>
    /// A folder with nothing to lose, which is an ordinary answer and not a missing one: a folder
    /// added a moment ago and not yet scanned has no titles, no marks and no minutes.
    /// </summary>
    public static LibraryRootRemovalSummary Empty { get; } = new(0, 0, TimeSpan.Zero);
}

/// <summary>
/// Reads what a removal would take. It has to reason about the same rows the removal then deletes,
/// or the warning promises one number and the removal does another.
/// </summary>
public interface ILibraryRootRemovalReader
{
    Task<LibraryRootRemovalSummary> SummarizeAsync(
        LibraryRootId id,
        CancellationToken cancellationToken = default);
}

public sealed class SummarizeLibraryRootRemoval
{
    private readonly ILibraryRootRemovalReader _reader;

    public SummarizeLibraryRootRemoval(ILibraryRootRemovalReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public Task<LibraryRootRemovalSummary> ExecuteAsync(
        LibraryRootId id,
        CancellationToken cancellationToken = default) =>
        _reader.SummarizeAsync(id, cancellationToken);
}
