using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// A request to play another version of the content that is already open. The structural flag says
/// whether the two versions are the same edit re-encoded or genuinely different cuts; restarting
/// from zero is the person's explicit answer to a transfer the policy would not make alone.
/// </summary>
public sealed record SwitchMediaVersionCommand(
    ContentKey Content,
    MediaVersion Target,
    bool StructureIsCompatible = true,
    bool Confirmed = false,
    bool RestartFromZero = false);

/// <summary>What the switch decided, whether it opened, and why it did not when it did not.</summary>
public sealed record SwitchMediaVersionResult(
    ProgressTransferDecision Decision,
    bool Opened,
    PlaybackFailure? Failure = null);

/// <summary>
/// Moves the session to another version of the same content. The previous position is written before
/// anything else happens, and stored progress is only rewritten once the new version is actually
/// playing, so a version that refuses to open costs nothing.
/// </summary>
public sealed class SwitchMediaVersion
{
    private readonly IWatchStateRepository _repository;
    private readonly IPlaybackSessionCoordinator _coordinator;
    private readonly PlaybackProgressTracker _tracker;
    private readonly IClock _clock;

    public SwitchMediaVersion(
        IWatchStateRepository repository,
        IPlaybackSessionCoordinator coordinator,
        PlaybackProgressTracker tracker,
        IClock clock)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<SwitchMediaVersionResult> ExecuteAsync(
        SwitchMediaVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Whatever happens next, the session that is ending keeps its position.
        _ = await _tracker
            .FlushAsync(PersistenceTrigger.FileChange, cancellationToken)
            .ConfigureAwait(false);

        var stored = await _repository.GetAsync(command.Content, cancellationToken).ConfigureAwait(false);
        var decision = ProgressTransferPolicy.Decide(
            stored?.Position ?? TimeSpan.Zero,
            stored?.ObservedDuration,
            command.Target.Duration,
            command.StructureIsCompatible);

        if (decision.Kind == ProgressTransferKind.Confirm && !command.Confirmed)
        {
            return new SwitchMediaVersionResult(decision, Opened: false);
        }

        // "Start again" is a person answering the question the policy raised: the switch happens,
        // and the point it lands on is zero rather than the transfer the policy proposed.
        if (command.RestartFromZero)
        {
            decision = decision with { Position = TimeSpan.Zero };
        }

        if (!command.Target.IsAvailable)
        {
            return new SwitchMediaVersionResult(
                decision,
                Opened: false,
                new PlaybackFailure(PlaybackFailureCode.FileNotFound, command.Target.Path));
        }

        try
        {
            _ = await _coordinator
                .StartAsync(
                    new PlaybackRequest(command.Target.MediaFileId, command.Target.Path, decision.Position),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PlaybackFailureException exception)
        {
            // Nothing was written for the new version, so the stored progress and its source are
            // still the ones the previous version left behind.
            return new SwitchMediaVersionResult(decision, Opened: false, exception.Failure);
        }

        await RecordAsync(command, decision, stored, cancellationToken).ConfigureAwait(false);
        _ = await _tracker
            .BeginAsync(command.Content, command.Target.MediaFileId, cancellationToken)
            .ConfigureAwait(false);
        return new SwitchMediaVersionResult(decision, Opened: true);
    }

    private async Task RecordAsync(
        SwitchMediaVersionCommand command,
        ProgressTransferDecision decision,
        WatchState? stored,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await _repository
            .SaveAsync(
                new WatchState
                {
                    Content = command.Content,
                    Position = decision.Position,
                    ObservedDuration = command.Target.Duration,
                    SourceMediaFileId = command.Target.MediaFileId,
                    Status = stored?.Status ?? WatchStatus.NotStarted,
                    IsManualOverride = stored?.IsManualOverride ?? false,
                    StartedUtc = stored?.StartedUtc ?? now,
                    UpdatedUtc = now,
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
