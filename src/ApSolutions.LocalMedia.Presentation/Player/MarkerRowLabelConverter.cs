// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Domain.Continuity;
using Avalonia.Data.Converters;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Turns a stored range into the one line a row of the player's side column has room for.
/// </summary>
/// <remarks>
/// <para>
/// Until 2026-08-21 neither marker list declared an <c>ItemTemplate</c>, so every row painted the
/// record's compiler-generated <c>ToString()</c>: <c>IntroMarker { Id = …, SeriesId = SeriesId
/// { Value = … }, Kind = Intro, Start = 00:00:30, … }</c>. Two GUIDs and a type name, in a column 320
/// wide, with no ellipsis and no tooltip. This is what those rows say instead.
/// </para>
/// <para>
/// It takes the record itself rather than a row view model, and that is the cheaper half of a real
/// choice: a row type would have moved <c>Markers</c> and <c>Detections</c> off the domain records
/// and dragged <c>SelectedMarker</c>, <c>Selected</c>, every existing test and the walk's anchors with
/// it. What the two lists needed was a label, not a new shape.
/// </para>
/// <para>
/// The separator is the one <c>QualityLabel</c> already uses in the same column, so two lists sitting
/// beside each other punctuate the same way. The range takes an en dash because it is a range and not
/// a subtraction.
/// </para>
/// <para>
/// <c>Confidence</c> is deliberately absent. It is the detector arguing with itself, and a percentage
/// on the row invites a person to argue back about a number they cannot act on — whereas
/// <c>UserCorrected</c> is the one thing on a detection that a person set and that changes what the
/// next detector run may touch, so that one is said.
/// </para>
/// </remarks>
public sealed class MarkerRowLabelConverter : IValueConverter
{
    private const string Separator = " · ";

    /// <summary>An en dash, because the two clocks are the ends of a range.</summary>
    private const string RangeDash = "–";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return value switch
        {
            IntroMarker marker => Label(marker.Kind, marker.Start, marker.End, confirmed: false),
            DetectedMarker detection =>
                Label(detection.Kind, detection.Start, detection.End, detection.UserCorrected),

            // The kind on its own, for the picker that chooses one. It goes through here rather than
            // through a second converter so the word in the picker is the same word as in the list.
            MarkerKind kind => Resource("MarkerKind" + kind),
            _ => string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A row label is read out of a range, never back into one.");

    private static string Label(MarkerKind kind, TimeSpan start, TimeSpan end, bool confirmed) =>
        string.Join(
            Separator,
            new[]
            {
                Resource("MarkerKind" + kind),
                Clock(start) + RangeDash + Clock(end),
                confirmed ? Resource("DetectedMarkerReviewConfirmed") : null,
            }.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>
    /// A position as a clock, with the hour only when there is one.
    /// </summary>
    /// <remarks>
    /// The invariant culture, and on purpose: what comes out is digits and colons, and a culture that
    /// wrote the separator differently would be describing a duration rather than a position in a
    /// film. The words around it are the part that follows the language.
    /// </remarks>
    private static string Clock(TimeSpan value) =>
        value.TotalHours >= 1
            ? value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : value.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// The words behind a key, asked of the application the way a theme-dependent resource has to be.
    /// </summary>
    /// <remarks>
    /// A key with nothing behind it comes back as the key rather than as an empty row: the row would
    /// otherwise lose the only thing that says which range it is, and a visible key is a defect
    /// somebody reports instead of a blank somebody explains away.
    /// </remarks>
    private static string Resource(string key)
    {
        var application = Avalonia.Application.Current;
        return application is not null
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
                ? value?.ToString() ?? key
                : key;
    }
}
