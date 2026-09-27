// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// Brings an HDR10 picture down to a standard display: the PQ curve of SMPTE ST 2084 undone, the
/// highlights rolled off with the EETF of Rec. ITU-R BT.2390, the BT.2020 primaries changed to
/// BT.709 with the matrix of BT.2087, and the result encoded for a display gamma of 2.4.
/// </summary>
/// <remarks>
/// <para>
/// <b>The standard display's white is 203 nits</b>, the HDR reference white of Rec. ITU-R BT.2408:
/// a sheet of paper in an HDR10 film is meant to sit there, so that is what becomes white here, and
/// everything brighter is what the roll-off makes room for. Mapping to 100 nits instead would leave
/// the whole film a stop darker than it was graded.
/// </para>
/// <para>
/// The work is done in two tables so the per-pixel cost is three reads, nine multiplications and
/// three more reads. The first turns a PQ signal level into light already rolled off, per channel —
/// which BT.2390 allows, and which is what keeps it a table. The second encodes linear light for the
/// display. Both are built once per film, never per frame.
/// </para>
/// </remarks>
public static class HdrToneCurve
{
    /// <summary>Levels of the PQ signal table: ten bits, which is what HDR10 carries.</summary>
    public const int SignalLevels = 1024;

    /// <summary>Levels of the encoding table, fine enough that no two neighbours share an output step twice.</summary>
    public const int EncodeLevels = 4096;

    /// <summary>The HDR reference white of BT.2408, which becomes the standard display's white.</summary>
    public const double SdrPeakNits = 203d;

    /// <summary>The peak assumed when the film does not declare a believable one: the usual mastering display.</summary>
    public const double DefaultSourcePeakNits = 1000d;

    private const double M1 = 2610d / 16384d;
    private const double M2 = 2523d / 4096d * 128d;
    private const double C1 = 3424d / 4096d;
    private const double C2 = 2413d / 4096d * 32d;
    private const double C3 = 2392d / 4096d * 32d;
    private const double DisplayGamma = 2.4d;

    /// <summary>
    /// BT.2020 to BT.709 on linear light, row by row, from Rec. ITU-R BT.2087.
    /// </summary>
    public static IReadOnlyList<double> Bt2020ToBt709 { get; } =
    [
        1.660491, -0.587641, -0.072850,
        -0.124551, 1.132900, -0.008349,
        -0.018151, -0.100579, 1.118730,
    ];

    /// <summary>Light in nits from a PQ signal between 0 and 1.</summary>
    public static double PqToNits(double signal)
    {
        var p = Math.Pow(Math.Clamp(signal, 0d, 1d), 1d / M2);
        return 10000d * Math.Pow(Math.Max(p - C1, 0d) / (C2 - (C3 * p)), 1d / M1);
    }

    /// <summary>The PQ signal between 0 and 1 that carries <paramref name="nits"/>.</summary>
    public static double NitsToPq(double nits)
    {
        var y = Math.Pow(Math.Clamp(nits, 0d, 10000d) / 10000d, M1);
        return Math.Pow((C1 + (C2 * y)) / (1d + (C3 * y)), M2);
    }

    /// <summary>
    /// The peak to roll off from: what the film declares when it is brighter than the display's white
    /// and no brighter than PQ can carry, the usual mastering peak otherwise.
    /// </summary>
    public static int SourcePeakNits(int? maxContentLight) =>
        maxContentLight is { } declared && declared > SdrPeakNits && declared <= 10000
            ? declared
            : (int)DefaultSourcePeakNits;

    /// <summary>
    /// Light for the standard display, as a fraction of its white, from one PQ signal level of a film
    /// whose brightest pixel is <paramref name="sourcePeakNits"/>.
    /// </summary>
    public static double Map(double signal, double sourcePeakNits)
    {
        var black = NitsToPq(0d);
        var range = NitsToPq(sourcePeakNits) - black;
        var level = Math.Clamp((signal - black) / range, 0d, 1d);
        var ceiling = (NitsToPq(SdrPeakNits) - black) / range;
        var knee = (1.5d * ceiling) - 0.5d;
        if (level > knee)
        {
            // BT.2390's Hermite spline from the knee to the ceiling.
            var t = (level - knee) / (1d - knee);
            var t2 = t * t;
            var t3 = t2 * t;
            level = (((2d * t3) - (3d * t2) + 1d) * knee)
                + ((t3 - (2d * t2) + t) * (1d - knee))
                + (((-2d * t3) + (3d * t2)) * ceiling);
        }

        return Math.Min(PqToNits((level * range) + black) / SdrPeakNits, 1d);
    }

    /// <summary>Rolled-off light for each of the <see cref="SignalLevels"/> PQ levels.</summary>
    public static float[] BuildSignalTable(int? maxContentLight)
    {
        var peak = SourcePeakNits(maxContentLight);
        var table = new float[SignalLevels];
        for (var level = 0; level < SignalLevels; level++)
        {
            table[level] = (float)Map(level / (double)(SignalLevels - 1), peak);
        }

        return table;
    }

    /// <summary>The eight-bit level for each of the <see cref="EncodeLevels"/> steps of linear light.</summary>
    public static byte[] BuildEncodeTable()
    {
        var table = new byte[EncodeLevels];
        for (var step = 0; step < EncodeLevels; step++)
        {
            table[step] = (byte)Math.Round(255d * Math.Pow(step / (double)(EncodeLevels - 1), 1d / DisplayGamma));
        }

        return table;
    }
}
