// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface IMediaProbe
{
    Task<TechnicalMetadata> ProbeAsync(string path, CancellationToken cancellationToken = default);
}
