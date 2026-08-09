using ApSolutions.LocalMedia.Application.Catalog;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed class CatalogItemViewModel(CatalogItem item)
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
}
