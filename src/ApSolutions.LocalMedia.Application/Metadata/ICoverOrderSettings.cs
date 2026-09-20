// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// The order the covers of the whole library are looked for in, as the one using it left it
/// (LIB-021, ADR-0009 decision 4).
/// </summary>
/// <remarks>
/// It is a port of its own and not a pair of calls to the settings store because the order is a
/// decision of <see cref="CoverOrderPolicy"/>: a store hands back whatever it holds, so reaching for
/// it directly would let a hand-edited file decide which picture a person sees.
/// </remarks>
public interface ICoverOrderSettings
{
    /// <summary>The stored order, always complete and walkable.</summary>
    IReadOnlyList<CoverOrigin> Current { get; }

    /// <summary>Stores an order, repaired first.</summary>
    void Save(IReadOnlyList<CoverOrigin> order);
}
