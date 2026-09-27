// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// What a video file declares about its colour, in the code points of Rec. ITU-T H.273 — the table
/// Matroska's <c>Colour</c> element and the MP4 <c>colr</c> box both carry. A null is a file that
/// declares nothing, which is not the same as one that declares the default.
/// </summary>
/// <param name="Primaries">H.273 <c>ColourPrimaries</c>: 1 is BT.709, 9 is BT.2020.</param>
/// <param name="Transfer">H.273 <c>TransferCharacteristics</c>: 16 is the PQ curve of HDR10.</param>
/// <param name="Matrix">H.273 <c>MatrixCoefficients</c>: 1 is BT.709, 5 and 6 are BT.601, 9 is BT.2020.</param>
/// <param name="MaxContentLight">The brightest pixel of the film in nits, when the file says.</param>
/// <remarks>
/// It exists because LibVLC 3 says nothing about colour: a video track publishes its geometry, its
/// cadence and its orientation and no more, measured by reflection over LibVLCSharp 3.10 on
/// 2026-09-27. So an HDR10 film was played as if it were standard range and shown as «SDR» — the
/// capability to recognise the curve existed, and nothing on the way to the screen ever called it.
/// </remarks>
public sealed record SourceColour(int? Primaries, int? Transfer, int? Matrix, int? MaxContentLight)
{
    /// <summary>H.273 transfer characteristic of SMPTE ST 2084, the PQ curve HDR10 is encoded with.</summary>
    public const int PerceptualQuantizer = 16;

    /// <summary>H.273 matrix coefficients of BT.2020 with non-constant luminance.</summary>
    public const int Bt2020NonConstantMatrix = 9;

    /// <summary>A file that declares nothing about its colour.</summary>
    public static SourceColour Undeclared { get; } = new(null, null, null, null);

    /// <summary>The HDR format the declaration amounts to. Only PQ is HDR10.</summary>
    /// <remarks>
    /// HLG (18) is left as standard range on purpose: it was designed to look right on a standard
    /// display without conversion, which is what this player shows it on.
    /// </remarks>
    public HdrFormat Hdr => Transfer == PerceptualQuantizer ? HdrFormat.Hdr10 : HdrFormat.None;
}
