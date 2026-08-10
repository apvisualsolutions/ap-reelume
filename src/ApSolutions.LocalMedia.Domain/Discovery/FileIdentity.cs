// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Discovery;

public sealed record FileIdentity(
    string? VolumeId,
    string? FileId,
    string? Fingerprint)
{
    public bool HasStableFileId =>
        !string.IsNullOrWhiteSpace(VolumeId) && !string.IsNullOrWhiteSpace(FileId);

    public bool HasFingerprint => !string.IsNullOrWhiteSpace(Fingerprint);
}

public enum ReconciliationDecision
{
    Exact,
    Probable,
    New,
    Missing,
}

public sealed record ReconciliationAssessment(
    ReconciliationDecision Decision,
    bool RequiresConfirmation,
    bool PreservesEntityIdentity,
    string Reason);
