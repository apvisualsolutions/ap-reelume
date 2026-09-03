// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>Why a chosen cover was refused, or that it was not.</summary>
public enum CoverImageVerdict
{
    /// <summary>The file may be copied into the application's own data.</summary>
    Approved,

    /// <summary>Nothing was chosen.</summary>
    NothingChosen,

    /// <summary>The extension is not one of the approved image containers.</summary>
    NotAnApprovedImage,

    /// <summary>The file is larger than a cover has any reason to be.</summary>
    TooLarge,

    /// <summary>The file is empty, which no image is.</summary>
    Empty,
}

/// <summary>
/// What may be accepted when somebody chooses their own cover (LIB-018), in one place.
/// </summary>
/// <remarks>
/// <b>This is the lock that has to exist before the door does.</b> Choosing a cover copies a file the
/// person picked into the application's own data — which is also what the backup carries — and until
/// 2026-09-03 the import that would receive it validated <b>neither the kind of file nor its
/// size</b>: it read the whole thing with no ceiling and wrote it out under whatever extension it
/// arrived with. The ten-megabyte ceiling beside it applied only to downloads.
/// <para>
/// <b>An allow-list and not a filter</b>, which is this repository's third rule: a filter has to
/// imagine in advance every kind of file that could go wrong, and an allow-list only has to name the
/// ones that are fine. It is written the way <see cref="MediaFileExtensions"/> is written, and for
/// its reason — the picker's dialog filter and the import's check must agree, and two lists drift.
/// </para>
/// </remarks>
public static class CoverImageRules
{
    /// <summary>
    /// The approved image containers, lower-case.
    /// </summary>
    /// <remarks>
    /// Four, and the absences are decisions. No <c>.svg</c>: it is a document that can carry script
    /// and remote references, and nothing about a cover needs one. No <c>.bmp</c> or <c>.tiff</c>: a
    /// cover that would be tens of megabytes uncompressed is a cover somebody should have exported
    /// first. No <c>.gif</c>: an animated grid is not what any of these surfaces draw.
    /// </remarks>
    public static IReadOnlyCollection<string> ApprovedExtensions { get; } =
        [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>
    /// The largest file that may be taken as a cover.
    /// </summary>
    /// <remarks>
    /// The same ten megabytes the remote cache already refuses beyond, and deliberately the same
    /// number rather than a second opinion: a cover is a cover whether it arrived over a wire or off
    /// a disk, and two ceilings for one thing is how one of them ends up forgotten. This is the one
    /// that was missing.
    /// </remarks>
    public const long MaximumBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> Approved =
        new(ApprovedExtensions, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the extension is one of the approved image containers, ignoring case.</summary>
    public static bool IsApprovedExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && Approved.Contains(extension);

    /// <summary>Whether this file may be taken as somebody's own cover.</summary>
    /// <remarks>
    /// It answers with a reason rather than with a boolean, because every one of these has to be
    /// said out loud to whoever picked the file: «no pasó nada» in front of a cover that did not
    /// change is the worst of the possible answers.
    /// <para>
    /// The extension is checked before the size on purpose. Both are refusals, but a person who
    /// picked a video by mistake is told what kind of file this takes, rather than being told their
    /// film is too big — which is true and useless.
    /// </para>
    /// </remarks>
    public static CoverImageVerdict Inspect(string? path, long lengthInBytes)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CoverImageVerdict.NothingChosen;
        }

        if (!IsApprovedExtension(Path.GetExtension(path)))
        {
            return CoverImageVerdict.NotAnApprovedImage;
        }

        if (lengthInBytes <= 0)
        {
            return CoverImageVerdict.Empty;
        }

        return lengthInBytes > MaximumBytes ? CoverImageVerdict.TooLarge : CoverImageVerdict.Approved;
    }
}
