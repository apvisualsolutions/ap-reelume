namespace ApSolutions.LocalMedia.Domain.Discovery;

public sealed class FileReconciliationPolicy
{
    private readonly StringComparer _identityComparer = StringComparer.OrdinalIgnoreCase;

    public ReconciliationAssessment Assess(
        FileIdentity? previous,
        FileIdentity? current,
        int matchingFingerprintCandidates,
        bool sourceAvailable)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(matchingFingerprintCandidates);
        if (!sourceAvailable || current is null)
        {
            return new ReconciliationAssessment(
                ReconciliationDecision.Missing,
                RequiresConfirmation: false,
                PreservesEntityIdentity: previous is not null,
                "SourceUnavailable");
        }

        if (previous is null)
        {
            return new ReconciliationAssessment(
                ReconciliationDecision.New,
                RequiresConfirmation: false,
                PreservesEntityIdentity: false,
                "NoCandidate");
        }

        if (previous.HasStableFileId &&
            current.HasStableFileId &&
            _identityComparer.Equals(previous.VolumeId, current.VolumeId) &&
            _identityComparer.Equals(previous.FileId, current.FileId))
        {
            return new ReconciliationAssessment(
                ReconciliationDecision.Exact,
                RequiresConfirmation: false,
                PreservesEntityIdentity: true,
                "StableFileId");
        }

        if (previous.HasFingerprint &&
            current.HasFingerprint &&
            _identityComparer.Equals(previous.Fingerprint, current.Fingerprint))
        {
            return matchingFingerprintCandidates <= 1
                ? new ReconciliationAssessment(
                    ReconciliationDecision.Exact,
                    RequiresConfirmation: false,
                    PreservesEntityIdentity: true,
                    "UniqueFingerprint")
                : new ReconciliationAssessment(
                    ReconciliationDecision.Probable,
                    RequiresConfirmation: true,
                    PreservesEntityIdentity: true,
                    "FingerprintCollision");
        }

        return new ReconciliationAssessment(
            ReconciliationDecision.New,
            RequiresConfirmation: false,
            PreservesEntityIdentity: false,
            "IdentityMismatch");
    }
}
