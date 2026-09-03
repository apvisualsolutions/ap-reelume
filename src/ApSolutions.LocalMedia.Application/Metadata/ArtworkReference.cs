// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>Where a piece of artwork came from, which is what decides whether it may be exported.</summary>
public enum ArtworkOrigin
{
    /// <summary>A file the person chose off their own disk. Theirs, so the backup carries it.</summary>
    Personal,

    /// <summary>Fetched from the provider. Not ours to redistribute, so the backup leaves it behind.</summary>
    RemoteCache,
}

/// <summary>Artwork that has landed on this disk: where it is, where it came from, and what it shows.</summary>
/// <remarks>
/// <b>It lived in Infrastructure until 2026-09-03 and that is why the port could not name it.</b>
/// <see cref="IArtworkStore"/> is an Application-layer port and dependencies point inwards, so a
/// port method returning a type from the adapter below it does not compile — which is exactly what
/// happened the moment anything tried to declare the personal import on the interface. The type was
/// never an implementation detail: it is what the operation answers, and it belongs beside the
/// contract that answers it.
/// </remarks>
public sealed record ArtworkReference(
    string Path,
    ArtworkOrigin Origin,
    string AlternativeText,
    bool IsExportable);
