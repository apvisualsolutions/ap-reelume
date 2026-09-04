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

    /// <summary>
    /// The file drawing this title's cover, or <see langword="null"/> when there is none to draw.
    /// </summary>
    /// <remarks>
    /// <b>Until 2026-09-04 this member did not exist, and neither did the picture.</b> The
    /// application downloaded covers from the provider and let somebody pick their own, stored both,
    /// and backed both up — and the grid everybody looks at drew a generated gradient over initials,
    /// because no card here had anywhere to put a real one. It is the same defect this repository
    /// already names as its own, sitting on the most looked-at screen in the application.
    /// <para>
    /// It answers <see langword="null"/> by default so a card with no cover to offer says so by
    /// saying nothing, which is what the rails on Home still do until they are given one.
    /// </para>
    /// </remarks>
    string? PosterFile => null;

    /// <summary>Whether there is a picture, which is what decides whether the initials show.</summary>
    bool HasPoster => !string.IsNullOrWhiteSpace(PosterFile);

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

    /// <summary>
    /// Whether the chip's shape is a screen rather than a strip of film.
    /// </summary>
    /// <remarks>
    /// Answered here, from the key the four models already give, so no model has to say it twice —
    /// and asked by a style rather than by a converter. A converter would have to reach for
    /// <c>Application.Current</c> and for a resource by name, which is two arms that cannot both be
    /// taken: both icon keys are declared and there is a gate over that inventory. A class on the
    /// glyph and two setters say the same thing with nothing to cover.
    /// </remarks>
    bool IsShow => string.Equals(KindKey, "CatalogKindShow", StringComparison.Ordinal);

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
