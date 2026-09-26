// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.UiTests.Fixtures;

/// <summary>
/// Numbers and shapes taken from the interactive design prototype the interface was drawn from.
/// </summary>
/// <remarks>
/// The prototype is not part of this repository, so the tests that need its values read them from
/// here. Each value is copied verbatim, and the copy is compared against the prototype itself on
/// the machines that have it; a value changed here without the prototype changing fails there.
/// </remarks>
internal static class PrototypeValues
{
    /// <summary>
    /// The row gap of the prototype's grid for each density. The grid writes two gaps per density
    /// (<c>'12px 10px'</c>, <c>'18px 16px'</c>, <c>'26px 22px'</c>) and the row one is the first.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> DensityRowGap =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["compact"] = 12,
            ["comfortable"] = 18,
            ["roomy"] = 26,
        };

    /// <summary>
    /// The padding of the row box, written beside its corner as <c>padding: '13px 16px'</c>.
    /// </summary>
    public static readonly (double Down, double Across) RowBoxPadding = (13, 16);

    /// <summary>Every size the prototype passes to its icon function.</summary>
    public static readonly IReadOnlySet<int> IconSizes = new HashSet<int> { 12, 13, 14, 15, 16, 18, 20, 22, 26 };

    /// <summary>
    /// The <c>d</c> attribute of every <c>path</c> of the prototype's pictograms that are made of
    /// paths alone, keyed by the name the prototype gives each one.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> IconPaths =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["home"] = ["M3 11 12 4l9 7", "M5.5 9.5V20h13V9.5"],
            ["play"] = ["M8 5.4 19 12 8 18.6Z"],
            ["back"] = ["M4 12a8 8 0 1 0 3-6.2", "M4 4.4v4.2h4.2"],
            ["fwd"] = ["M20 12a8 8 0 1 1-3-6.2", "M20 4.4v4.2h-4.2"],
            ["vol"] = ["M4 9.5h3.6L12 6v12L7.6 14.5H4z", "M15.6 9.6a4 4 0 0 1 0 4.8", "M18 7.6a7 7 0 0 1 0 8.8"],
            ["mute"] = ["M4 9.5h3.6L12 6v12L7.6 14.5H4z", "M16 10l4 4", "M20 10l-4 4"],
            ["full"] = ["M4 9V4h5", "M20 15v5h-5", "M15 4h5v5", "M9 20H4v-5"],
            ["exitfull"] = ["M9 4v5H4", "M15 20v-5h5", "M20 9h-5V4", "M4 15h5v5"],
            ["close"] = ["M6.2 6.2l11.6 11.6", "M17.8 6.2 6.2 17.8"],
            ["chev"] = ["M9.5 5.5 16 12l-6.5 6.5"],
            ["chevd"] = ["M5.5 9 12 15.5 18.5 9"],
            ["warn"] = ["M12 4.6 20.8 19.4H3.2z", "M12 10v4", "M12 16.6v.1"],
            ["check"] = ["M5.5 12.4 10 16.9 18.6 7.6"],
            ["plus"] = ["M12 5.5v13", "M5.5 12h13"],
            ["mark"] = ["M7 4h10v16l-5-4-5 4z"],
            ["ext"] = ["M14 4h6v6", "M20 4l-8.5 8.5", "M18 14.5V20H4V6h5.5"],
        };
}
