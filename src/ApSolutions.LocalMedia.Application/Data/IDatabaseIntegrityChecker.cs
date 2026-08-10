// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Data;

public sealed record DatabaseIntegrityResult(bool IsValid, string Detail);

public interface IDatabaseIntegrityChecker
{
    Task<DatabaseIntegrityResult> CheckAsync(CancellationToken cancellationToken = default);
}
