// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface IMediaProbe
{
    Task<TechnicalMetadata> ProbeAsync(string path, CancellationToken cancellationToken = default);
}
