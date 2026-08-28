// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
/// <b>The cache is bounded, and that is not a detail.</b> A decoded <c>w780</c> poster is 780×1170
/// at four bytes a pixel, which is about 3.5 MB in memory whatever the file on disk weighs. An
/// unbounded dictionary keyed by path would hold one of those for every title anybody opened, so a
/// person browsing a hundred films would be carrying a third of a gigabyte of pictures nothing is
/// drawing any more. <see cref="Capacity"/> entries is what a walk back through recently opened
/// cards needs — both surfaces of one card share a single entry — and the oldest is dropped past it.
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
    /// <summary>How many decoded posters are kept. See the remarks for why there is a number here.</summary>
    public const int Capacity = 8;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, LinkedListNode<CacheEntry>> Decoded =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Most recently used first, so the one to drop is always the last.</summary>
    private static readonly LinkedList<CacheEntry> Recency = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value is string path && !string.IsNullOrWhiteSpace(path) ? Remember(path) : null;
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

    private static Bitmap? Remember(string path)
    {
        lock (Gate)
        {
            if (Decoded.TryGetValue(path, out var known))
            {
                Recency.Remove(known);
                Recency.AddFirst(known);
                return known.Value.Picture;
            }

            // A path that decoded to nothing is remembered as nothing, on purpose: without it, a
            // deleted file would be opened and refused again on every layout pass of every card that
            // names it.
            var entry = new LinkedListNode<CacheEntry>(new CacheEntry(path, Decode(path)));
            Recency.AddFirst(entry);
            Decoded[path] = entry;
            // Dropped and not disposed, deliberately. Both cards stay mounted while the shell shows
            // one of them, so an Image can still be holding the picture that is being evicted — and
            // a disposed bitmap under a control that draws it is a crash, where a dropped reference
            // is only a decode somebody may pay for again. Letting go is what bounds the memory;
            // the collector does the rest once no view is pointing at it.
            if (Recency.Last is { } oldest && Decoded.Count > Capacity)
            {
                Recency.RemoveLast();
                _ = Decoded.Remove(oldest.Value.Path);
            }

            return entry.Value.Picture;
        }
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

    private sealed record CacheEntry(string Path, Bitmap? Picture);
}
