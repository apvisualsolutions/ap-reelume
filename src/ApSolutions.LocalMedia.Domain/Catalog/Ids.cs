// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Domain.Catalog;

public readonly record struct TitleId(Guid Value);

public readonly record struct MediaFileId(Guid Value);

public readonly record struct MediaVersionId(Guid Value);

public readonly record struct LibraryRootId(Guid Value);

public readonly record struct SeriesId(Guid Value);

public readonly record struct EpisodeId(Guid Value);
