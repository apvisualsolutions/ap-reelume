// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

namespace ApSolutions.LocalMedia.Application.Data;

public sealed record DatabaseIntegrityResult(bool IsValid, string Detail);

public interface IDatabaseIntegrityChecker
{
    Task<DatabaseIntegrityResult> CheckAsync(CancellationToken cancellationToken = default);
}
