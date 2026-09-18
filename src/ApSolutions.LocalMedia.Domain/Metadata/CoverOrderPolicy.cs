// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>Where a title's cover came from (ADR-0009, LIB-021).</summary>
public enum CoverOrigin
{
    /// <summary>A file somebody picked from their own disk.</summary>
    Personal,

    /// <summary>The artwork the metadata provider offered.</summary>
    Provider,

    /// <summary>
    /// A frame of the title's own video, taken by the application when there is no other cover.
    /// </summary>
    Frame,
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
    /// <summary>The picked cover wins; the provider's follows; the frame is last.</summary>
    public static IReadOnlyList<CoverOrigin> Default { get; } =
        [CoverOrigin.Personal, CoverOrigin.Provider, CoverOrigin.Frame];
}
