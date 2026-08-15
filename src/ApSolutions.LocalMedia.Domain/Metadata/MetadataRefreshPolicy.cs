// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>
/// When a stored entry is old enough to ask the provider about again, and how many are asked about
/// in one pass.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StaleAfter"/> has to stay <b>below</b> the provider cache's retention ceiling: a copy
/// that may no longer exist at all cannot be the copy an automatic refresh is deciding about, so the
/// window for asking again closes before the window for keeping anything does. A test holds that
/// inequality, because the two numbers live in different layers and nothing else would notice them
/// crossing.
/// </para>
/// <para>
/// <see cref="MaximumPerPass"/> is what contains the first pass over a whole library. Without it,
/// turning the switch on once would spend a library's worth of requests in a single launch.
/// </para>
/// </remarks>
public static class MetadataRefreshPolicy
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(90);

    public const int MaximumPerPass = 20;

    /// <summary>
    /// True for an entry old enough to ask about again. <paramref name="refreshedUtc"/> being absent
    /// counts as stale, and as the stalest there is: an entry with no date was never refreshed. No
    /// production path writes that combination today — measured on 2026-08-15, an identified row
    /// always carries the moment its provider answered — so this is the guard for the row nothing
    /// currently writes, not a case in the field.
    /// </summary>
    public static bool IsStale(DateTimeOffset? refreshedUtc, DateTimeOffset now) =>
        refreshedUtc is not { } refreshed || now - refreshed >= StaleAfter;

    /// <summary>The moment an entry has to predate to count as stale.</summary>
    public static DateTimeOffset StaleBefore(DateTimeOffset now) => now - StaleAfter;
}
