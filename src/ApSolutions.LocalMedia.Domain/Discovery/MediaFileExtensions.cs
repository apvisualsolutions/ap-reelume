// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Discovery;

/// <summary>
/// The containers this release recognises, in one place.
/// <para>
/// The scanner and loose-file activation must agree: a file the library would catalogue has to be a
/// file "Open with…" will play, and the reverse. Two lists would drift.
/// </para>
/// </summary>
public static class MediaFileExtensions
{
    /// <summary>
    /// The approved containers, lower-case and ordered as the specification lists them.
    /// </summary>
    /// <remarks>
    /// <c>.flv</c> is the one addition after the MVP specification, so it sits at the end rather than
    /// in alphabetical or any other order: three files and several pieces of archived evidence quote
    /// this sequence, and the packaging suite compares them <b>in order</b>. It was added on
    /// 2026-08-31 because it is the only video container the application did not recognise across the
    /// owner's two course roots — ten files, one whole course invisible — while the eight above cover
    /// 98.3 % of his video, measured.
    /// </remarks>
    public static IReadOnlyCollection<string> All { get; } =
        [".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts", ".m2ts", ".flv"];

    /// <summary>
    /// The same list as a set, built from <see cref="All"/> rather than written a second time: this
    /// class exists because two lists drift, and a class that keeps two of its own would be the first
    /// place it happened.
    /// </summary>
    private static readonly HashSet<string> Approved = new(All, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the extension belongs to the approved set, ignoring case.</summary>
    public static bool IsApproved(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && Approved.Contains(extension);
}
