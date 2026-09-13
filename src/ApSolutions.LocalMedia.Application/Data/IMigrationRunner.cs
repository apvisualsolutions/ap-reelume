// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

namespace ApSolutions.LocalMedia.Application.Data;

public interface IMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
