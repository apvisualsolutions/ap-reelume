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

    /// <summary>The line under the title: a year, or a season and episode.</summary>
    string CaptionText { get; }

    /// <summary>Whether there is a caption at all; an empty line would still take its height.</summary>
    bool HasCaption { get; }

    /// <summary>Whether <see cref="CompletedFraction"/> is a number this list actually read.</summary>
    bool HasKnownProgress { get; }

    /// <summary>How much of it has been watched, from 0 to 1.</summary>
    double CompletedFraction { get; }
}
