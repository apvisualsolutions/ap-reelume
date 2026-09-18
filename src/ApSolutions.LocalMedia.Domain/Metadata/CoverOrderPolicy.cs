// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>
/// Where a title's cover came from (ADR-0009, LIB-021). The frame taken from the video itself is the
/// third origin, and joins when its capture is wired for films and series.
/// </summary>
public enum CoverOrigin
{
    /// <summary>A file somebody picked from their own disk.</summary>
    Personal,

    /// <summary>The artwork the metadata provider offered.</summary>
    Provider,
}

/// <summary>
/// The order in which a title's covers are asked for: the first origin with a file on disk draws.
/// </summary>
/// <remarks>
/// The general setting and the per-title override the ADR decides will replace this list, never the
/// loop that walks it — so the day they arrive there is still one place that decides which picture a
/// person sees.
/// </remarks>
public static class CoverOrderPolicy
{
    /// <summary>The picked cover wins; the provider's follows.</summary>
    public static IReadOnlyList<CoverOrigin> Default { get; } = [CoverOrigin.Personal, CoverOrigin.Provider];
}
