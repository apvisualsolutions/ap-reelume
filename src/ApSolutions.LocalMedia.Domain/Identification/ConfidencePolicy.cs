namespace ApSolutions.LocalMedia.Domain.Identification;

public static class ConfidencePolicy
{
    public const double AutomaticThreshold = 0.90;
    public const double SuggestedThreshold = 0.60;

    public static ReviewState Classify(double score)
    {
        if (!double.IsFinite(score) || score is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score));
        }

        if (score >= AutomaticThreshold)
        {
            return ReviewState.Automatic;
        }

        return score >= SuggestedThreshold
            ? ReviewState.Suggested
            : ReviewState.Pending;
    }
}
