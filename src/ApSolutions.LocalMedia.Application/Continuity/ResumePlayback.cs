using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>What the session should do with stored progress.</summary>
public enum ResumeChoice
{
    Resume,
    Restart,
}

/// <summary>The offer made before opening: a position to resume from, or a restart from zero.</summary>
public sealed record ResumeDecision(ResumeChoice Choice, TimeSpan Position);

/// <summary>
/// Reads stored progress and decides whether it is worth offering. Trivial progress near the start is
/// never offered, so the prompt does not appear for a file that was opened and closed.
/// </summary>
public sealed class ResumePlayback
{
    private readonly IWatchStateRepository _repository;

    public ResumePlayback(IWatchStateRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<ResumeDecision> DecideAsync(
        ContentKey content,
        TimeSpan? duration,
        CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAsync(content, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return new ResumeDecision(ResumeChoice.Restart, TimeSpan.Zero);
        }

        var observed = duration ?? stored.ObservedDuration;
        var position = ProgressPolicy.ClampPosition(stored.Position, observed);
        return ProgressPolicy.ShouldOfferResume(position, observed)
            ? new ResumeDecision(ResumeChoice.Resume, position)
            : new ResumeDecision(ResumeChoice.Restart, TimeSpan.Zero);
    }
}
