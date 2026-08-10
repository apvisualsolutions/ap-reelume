// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Continuity;

/// <summary>
/// A range the detector found in one episode. Manual markers are per series; a detection is per
/// episode file, because a variable cold open moves the intro around in every episode and one fixed
/// range per series cannot represent that.
/// </summary>
public sealed record DetectedMarker(
    Guid Id,
    SeriesId SeriesId,
    MediaFileId FileId,
    MarkerKind Kind,
    TimeSpan Start,
    TimeSpan End,
    double Confidence,
    int DetectorVersion,
    bool UserCorrected);
