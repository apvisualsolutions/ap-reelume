// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Domain.Discovery;

public interface IFileIdentityProvider
{
    Task<FileIdentity> GetAsync(
        string path,
        TechnicalMetadata technicalMetadata,
        CancellationToken cancellationToken = default);
}
