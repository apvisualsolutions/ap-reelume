// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// What one 2:3 card shows, whichever list it is in.
/// </summary>
/// <remarks>
/// <para>
/// The card repeats in four places — the library grid and the three rails on Home — backed by four
/// different view models. An interface rather than <c>UnavailableBadge</c>'s reflection bindings,
/// because there the six mounting view models had nothing else in common; here they do, and a
/// compiled binding refuses at build time what a reflection binding discovers as a blank card.
/// It also makes the omissions explicit: a rail that does not know whether its medium is reachable
/// cannot silently paint "available".
/// </para>
/// <para>
/// <see cref="HasKnownProgress"/> is separate from having progress on purpose. The catalogue knows
/// that a title has been started — <c>CatalogItem.HasProgress</c> — and does not know how far, and a
/// bar drawn at zero for something half watched is a worse answer than no bar.
/// </para>
/// </remarks>
public interface IPosterCard
{
    /// <summary>The title, which the card gives at most two lines.</summary>
    string Title { get; }

    /// <summary>The letters standing in for artwork; see <see cref="PosterInitials"/>.</summary>
    string Initials { get; }

    /// <summary>Whether <see cref="CompletedFraction"/> is a number this list actually read.</summary>
    bool HasKnownProgress { get; }

    /// <summary>How much of it has been watched, from 0 to 1.</summary>
    double CompletedFraction { get; }

    /// <summary>
    /// The word in the chip at the top left of the cover, as a resource key: film or series.
    /// </summary>
    /// <remarks>
    /// A key and not a word, like every other string a view model hands over here: the chip says
    /// «Película» in one language and "Film" in the other, and deciding that in a model would put a
    /// language inside something that has none. The five below follow the same rule where they can —
    /// the running time and the genres are the title's own data and carry no wording at all.
    /// </remarks>
    string KindKey { get; }

    /// <summary>Whether there is a chip to draw at all; a rail that cannot say has none.</summary>
    bool HasKind { get; }

    /// <summary>The line under the title: «2024 · 111 min · Suspense», already joined.</summary>
    string MetaText { get; }

    /// <summary>Whether there is a meta line at all.</summary>
    bool HasMeta { get; }

    /// <summary>
    /// The line under that one, as a resource key: not started, in progress, watched — or nothing,
    /// when the card counts episodes instead.
    /// </summary>
    string StatusKey { get; }

    /// <summary>«10/16», for a series; empty for anything else.</summary>
    string EpisodeCountText { get; }

    /// <summary>Whether this card counts episodes rather than naming a status.</summary>
    bool CountsEpisodes { get; }

    /// <summary>Whether the whole thing has been seen, which the cover marks with a tick.</summary>
    bool IsWatched { get; }

    /// <summary>Whether the file behind it is reachable; the cover says so when it is not.</summary>
    bool IsAvailable { get; }
}
