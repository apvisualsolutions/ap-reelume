// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>What a card on one of Home's rails has to be able to say to be opened.</summary>
/// <remarks>
/// Both rails carry a title id and neither carried a way to use it: their cards were list items and
/// pressing one selected it. The prototype gives every card a Detalles button, and one command on
/// the page serves the whole rail — which needs the cards to agree on this much and nothing more.
/// </remarks>
public interface IRailCard
{
    /// <summary>The title this card stands for, which is what its card is opened by.</summary>
    TitleId TitleId { get; }
}
