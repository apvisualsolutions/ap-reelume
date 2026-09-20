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
/// The general setting and the per-title override the ADR decides replace this list, never the loop
/// that walks it — so there is still one place that decides which picture a person sees. Both arrive
/// through <see cref="Normalize"/>, which is why neither can hand the loop something it cannot walk.
/// </remarks>
public static class CoverOrderPolicy
{
    /// <summary>The picked cover wins; the provider's follows; the frame is last.</summary>
    public static IReadOnlyList<CoverOrigin> Default { get; } =
        [CoverOrigin.Personal, CoverOrigin.Provider, CoverOrigin.Frame];

    /// <summary>
    /// Turns any list into one that can be walked: every origin exactly once, in the order given,
    /// with the ones left out added at the end in the default order.
    /// </summary>
    /// <remarks>
    /// A stored order is repaired on the way out rather than handed to the application, the way
    /// <c>AppLifecyclePolicy.Normalize</c> does with a settings file somebody edited. An order
    /// missing an origin would make a cover that exists unreachable with nothing saying so, and one
    /// naming a value outside the enum would walk into the resolver's switch.
    /// </remarks>
    public static IReadOnlyList<CoverOrigin> Normalize(IReadOnlyList<CoverOrigin>? order)
    {
        if (order is null || order.Count == 0) { return Default; }

        var normalized = new List<CoverOrigin>(Default.Count);
        foreach (var origin in order)
        {
            if (Enum.IsDefined(origin) && !normalized.Contains(origin)) { normalized.Add(origin); }
        }

        foreach (var origin in Default)
        {
            if (!normalized.Contains(origin)) { normalized.Add(origin); }
        }

        return normalized;
    }

    /// <summary>Writes an order as the text a settings file or a catalogue column holds.</summary>
    public static string Format(IReadOnlyList<CoverOrigin> order) => string.Join(',', Normalize(order));

    /// <summary>
    /// Reads an order back.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> for anything that does not name origins; <paramref name="order"/> is
    /// the default either way, so a caller that ignores the verdict still gets a walkable list
    /// rather than an empty one that would draw nothing.
    /// </returns>
    public static bool TryParse(string? text, out IReadOnlyList<CoverOrigin> order)
    {
        order = Default;
        if (string.IsNullOrWhiteSpace(text)) { return false; }

        var parsed = new List<CoverOrigin>(Default.Count);
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Enum.TryParse<CoverOrigin>(part, ignoreCase: false, out var origin) || !Enum.IsDefined(origin))
            {
                return false;
            }

            parsed.Add(origin);
        }

        if (parsed.Count == 0) { return false; }

        order = Normalize(parsed);
        return true;
    }

    /// <summary>
    /// The order a single title gets when somebody picks one origin for it: that origin wins and the
    /// rest keep the places they had.
    /// </summary>
    /// <remarks>
    /// The whole order is kept and not just the winner, which is what makes the choice survive a
    /// later change to the general order — an override that quietly followed it would stop meaning
    /// what it meant the day it was set.
    /// </remarks>
    public static IReadOnlyList<CoverOrigin> WithFirst(CoverOrigin first, IReadOnlyList<CoverOrigin>? rest)
    {
        var order = new List<CoverOrigin>(Default.Count) { first };
        order.AddRange(Normalize(rest));
        return Normalize(order);
    }
}
