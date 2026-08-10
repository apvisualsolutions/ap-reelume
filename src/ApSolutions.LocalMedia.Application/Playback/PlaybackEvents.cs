// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>The single embedded playback session that the coordinator keeps alive.</summary>
public sealed record PlaybackSession(Guid Id, MediaFileId MediaFileId, string Path);

/// <summary>Typed application event describing every observable change of the active session.</summary>
public sealed record PlaybackSessionChanged(
    Guid SessionId,
    MediaFileId MediaFileId,
    PlaybackState State,
    TimeSpan Position,
    PlaybackFailure? Failure = null);
