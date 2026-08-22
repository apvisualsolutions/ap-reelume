// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Catalog;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed class CatalogItemViewModel(CatalogItem item) : IPosterCard
{
    public CatalogItem Item { get; } = item ?? throw new ArgumentNullException(nameof(item));

    public string Title => Item.Title;

    public int? Year => Item.Year;

    public bool IsAvailable => Item.IsAvailable;

    /// <summary>
    /// The availability, as a resource key the surface translates. A reader hears the title and then
    /// whether the file is reachable, without the view model deciding any wording.
    /// </summary>
    public string AvailabilityKey => IsAvailable ? "MediaAvailable" : "MediaUnavailable";

    public string Initials => PosterInitials.From(Title);

    public string CaptionText => Year is { } year
        ? year.ToString(CultureInfo.CurrentCulture)
        : string.Empty;

    public bool HasCaption => Year is not null;

    /// <summary>
    /// Always false, and not because the catalogue has nothing to say about progress.
    /// </summary>
    /// <remarks>
    /// <c>CatalogItem.HasProgress</c> is a flag the filter reads: it says a title has been started
    /// and not how far it got. Painting a bar from it would draw every half-watched film at zero,
    /// which is a worse answer than no bar. Carrying the fraction this far is a change through the
    /// read model, the query and SQLite, and it belongs with the artwork rather than with the card.
    /// </remarks>
    public bool HasKnownProgress => false;

    public double CompletedFraction => 0;
}
