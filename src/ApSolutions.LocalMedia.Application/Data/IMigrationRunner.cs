// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Data;

public interface IMigrationRunner
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
