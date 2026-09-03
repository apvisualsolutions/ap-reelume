// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>What became of a cover somebody chose: where it landed, or why it was refused.</summary>
/// <remarks>
/// The verdict travels with the answer rather than being thrown, because every one of these has to
/// reach the person who picked the file. A refusal that arrives as an exception becomes a log line,
/// and a cover that did not change with nothing said about it is the worst of the possible outcomes.
/// </remarks>
public sealed record PersonalCoverResult(CoverImageVerdict Verdict, string? Path)
{
    public bool Succeeded => Verdict == CoverImageVerdict.Approved && !string.IsNullOrWhiteSpace(Path);
}

/// <summary>
/// Takes a file the person chose and makes it this title's cover (LIB-018).
/// </summary>
/// <remarks>
/// <b>This is the call that did not exist.</b> The store has known how to import a personal image
/// since the artwork work landed; the backup carries the folder it writes to and refuses to delete
/// it when clearing what was downloaded; the picker's view model has a property to hold the answer.
/// Nothing called it. Measured on 2026-09-03: the only callers of
/// <see cref="IArtworkStore.ImportPersonalAsync"/> in the whole tree were two tests.
/// <para>
/// It reads the disk and asks nobody anything: a cover chosen from a folder is the one kind of
/// artwork this application never has to go and look up.
/// </para>
/// </remarks>
public sealed class SetPersonalCover(IArtworkStore store)
{
    private readonly IArtworkStore _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>Copies the chosen file into this title's artwork, or says why it will not.</summary>
    /// <remarks>
    /// The file is inspected before it is opened, so a refusal costs a stat rather than a read. The
    /// store checks again on its own side, and the repetition is deliberate: this guards the caller
    /// that exists, and the store guards the ones written later.
    /// </remarks>
    public async Task<PersonalCoverResult> ExecuteAsync(
        TitleId titleId,
        string? sourcePath,
        string alternativeText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return new PersonalCoverResult(CoverImageVerdict.NothingChosen, null);
        }

        var file = new FileInfo(sourcePath);
        var verdict = CoverImageRules.Inspect(sourcePath, file.Exists ? file.Length : 0);
        if (verdict != CoverImageVerdict.Approved)
        {
            return new PersonalCoverResult(verdict, null);
        }

        var reference = await _store
            .ImportPersonalAsync(titleId, sourcePath, alternativeText, cancellationToken)
            .ConfigureAwait(false);

        return new PersonalCoverResult(CoverImageVerdict.Approved, reference.Path);
    }
}
