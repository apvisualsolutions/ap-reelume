// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

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

    /// <summary>
    /// True once the catalogue has a fraction to draw, which it has had since 2026-08-24.
    /// </summary>
    /// <remarks>
    /// It used to be false with a note saying why: <c>HasProgress</c> is a flag the filter reads —
    /// started or not — and a bar drawn from it would put every half-watched film at zero. The note
    /// ended "carrying the fraction this far is a change through the read model, the query and
    /// SQLite", and that is the change the prototype's card finally asked for.
    /// </remarks>
    public bool HasKnownProgress => Item.CompletedFraction > 0;

    public double CompletedFraction => Item.CompletedFraction;

    public string KindKey => Item.Kind switch
    {
        CatalogTitleKind.Movie => "CatalogKindMovie",
        CatalogTitleKind.Show => "CatalogKindShow",
        _ => "CatalogKindFile",
    };

    public bool HasKind => true;

    /// <summary>«2024 · 111 min · Suspense», in the prototype's own order and separator.</summary>
    /// <remarks>
    /// Built here and not in the view because it is three optional pieces: a file with no year, no
    /// length and no genre would leave a line of separators behind, and a view cannot drop those
    /// without knowing which piece is missing.
    /// </remarks>
    public string MetaText => string.Join(
        " · ",
        new[]
        {
            Year?.ToString(CultureInfo.CurrentCulture) ?? string.Empty,
            Item.Runtime is { } runtime && runtime > TimeSpan.Zero
                // Substituted rather than formatted: the pattern comes from a dictionary that
                // changes with the language, so a cached CompositeFormat — which is what the
                // analyser asks for — would be a cache of whichever language happened to load first.
                ? Resource("CatalogRuntimeMinutes", "{0} min").Replace(
                    "{0}",
                    ((int)Math.Round(runtime.TotalMinutes)).ToString(CultureInfo.CurrentCulture),
                    StringComparison.Ordinal)
                : string.Empty,
            Item.Genres is { Count: > 0 } genres ? string.Join(" · ", genres.Take(2)) : string.Empty,
        }.Where(piece => piece.Length > 0));

    public bool HasMeta => MetaText.Length > 0;

    public string StatusKey => Item.Status switch
    {
        WatchStatus.InProgress => "WatchStatusInProgress",
        WatchStatus.Watched => "WatchStatusWatched",
        _ => "WatchStatusNotStarted",
    };

    /// <summary>«10/16» for a series, and nothing at all for anything else.</summary>
    public string EpisodeCountText => CountsEpisodes
        ? string.Create(CultureInfo.CurrentCulture, $"{Item.EpisodesWatched}/{Item.EpisodeCount}")
        : string.Empty;

    public bool CountsEpisodes => Item.Kind == CatalogTitleKind.Show && Item.EpisodeCount > 0;

    public bool IsWatched => Item.Status == WatchStatus.Watched;

    /// <summary>
    /// The words behind a key, resolved where the resources are, with the key's own fallback.
    /// </summary>
    /// <remarks>
    /// One string in this model needs formatting rather than picking — «{0} min» — so it is the one
    /// place a view model reaches for a resource. The fallback is not decoration: a headless test
    /// mounts this without the string dictionaries, and a null there would print an empty running
    /// time rather than failing loudly.
    /// </remarks>
    private static string Resource(string key, string fallback) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            && value is string text
                ? text
                : fallback;
}
