// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// Turns the path of a cached poster into the picture a view can draw, or into nothing.
/// </summary>
/// <remarks>
/// <para>
/// A converter rather than a <c>Bitmap</c> on the view model, so that a view model stays something a
/// test can build with no graphics stack behind it — and so the two surfaces that draw the same
/// poster, the raised card and the wall behind it, decode it once between them.
/// </para>
/// <para>
/// <b>Nothing here throws.</b> The path names a file this application wrote into its own cache, and
/// by the time a card is opened that file may have been deleted by hand, half written, or not be an
/// image at all. Every one of those is "no poster", which is a state this card already draws: the
/// generated art is underneath, always, and it is what an unidentified library shows anyway.
/// </para>
/// </remarks>
public sealed class CachedPosterConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, Bitmap?> Decoded =
        new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is string path && !string.IsNullOrWhiteSpace(path)
            ? Decoded.GetOrAdd(path, Decode)
            : null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = value;
        _ = targetType;
        _ = parameter;
        _ = culture;

        // One way only. A view has no business writing a poster back, and a converter that pretended
        // to would be a second place that decides where artwork lives.
        throw new NotSupportedException("A poster is drawn, never written back.");
    }

    private static Bitmap? Decode(string path)
    {
        try
        {
            return new Bitmap(path);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException)
        {
            return null;
        }
    }
}
