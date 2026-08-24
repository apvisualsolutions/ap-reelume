// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using Avalonia.Data.Converters;

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// The shape that goes in the kind chip, from the key that names the kind.
/// </summary>
/// <remarks>
/// The prototype draws a small frame beside the word — a film strip for a film, a screen for a
/// series — and picks it with the same expression that picks the word. A converter rather than a
/// property on the card, for the same reason <see cref="PosterArtConverter"/> is one: which picture
/// a kind gets is a fact about the kind, not a decision any of the four view models behind a card
/// makes, and putting it on the interface would be four copies of one switch.
/// </remarks>
public sealed class KindShapeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;

        var key = (value as string) switch
        {
            "CatalogKindShow" => "IconShow",
            _ => "IconFilm",
        };

        return Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var shape)
                ? shape
                : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A shape does not become a kind.");
}
