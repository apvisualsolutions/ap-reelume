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
/// <para>
/// <b>And the decode is bounded, since 2026-09-04.</b> Until then every poster here came from the
/// provider and was <c>w780</c> by construction, so the bound above held by luck rather than by
/// rule. A cover somebody chooses off their own disk is whatever their camera produced: ten
/// megabytes of JPEG is tens of millions of pixels, which at four bytes each is the difference
/// between the 3.5 MB this class budgets and something nearer seventy — eight of those, on the
/// thread that draws. Measured on Avalonia 12.1.1: a 2000×3000 source decoded whole is 24 MB and
/// bounded is 3.65 MB. The bound is paid on the way in too, and honestly: a 300×450 cover is
/// <em>enlarged</em> to 780×1170, so it costs 3.65 MB where it would have cost 0.5. That is the
/// trade — a fixed cost per entry instead of one nobody can predict — and it is the cost this
/// class was already written around.
/// </para>
/// </remarks>
public sealed class CachedPosterConverter : IValueConverter
{
    /// <summary>How many decoded posters are kept. See the remarks for why there is a number here.</summary>
    public const int Capacity = 8;

    /// <summary>
    /// The width every poster is decoded at, which is the width the provider already served.
    /// </summary>
    /// <remarks>
    /// The same 780 as <c>PosterAddressPolicy.Size</c>, and deliberately the same number rather than
    /// a second opinion: the raised card is 158 wide and the header bleed about 1,180, so one size
    /// covers both, and two sizes for one picture is how one of them ends up forgotten.
    /// </remarks>
    public const int DecodeWidth = 780;

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
            using var file = File.OpenRead(path);
            return Bitmap.DecodeToWidth(file, DecodeWidth);
        }
        // NullReferenceException is in this list because it was measured, not because anything here
        // is careless. On Avalonia 12.1.1 the whole-file constructor answers an undecodable file with
        // ArgumentException — «Unable to load bitmap from provided data» — while DecodeToWidth throws
        // a NullReferenceException from inside Avalonia.Skia.ImmutableBitmap for the very same file.
        // The bound above is worth having and this is its price; the try holds two statements and
        // nothing of ours can raise it, so what is being swallowed is unambiguous.
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or NullReferenceException)
        {
            return null;
        }
    }

    private sealed record CacheEntry(string Path, Bitmap? Picture);
}
