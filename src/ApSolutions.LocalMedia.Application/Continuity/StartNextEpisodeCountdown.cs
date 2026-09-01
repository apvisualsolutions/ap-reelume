// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>How a countdown ended.</summary>
public enum NextEpisodeOutcome
{
    /// <summary>The next episode is playing.</summary>
    Started,

    /// <summary>Someone stopped the countdown before it finished.</summary>
    Cancelled,

    /// <summary>The person turned the chain off, so nothing was offered.</summary>
    Disabled,

    /// <summary>The episode stopped being playable before it could be opened.</summary>
    Unavailable,

    /// <summary>There is nothing after this episode; the shell returns to the details.</summary>
    NoNextEpisode,
}

/// <summary>What happened and which episode it was about.</summary>
public sealed record NextEpisodeResult(NextEpisodeOutcome Outcome, EpisodeSequenceEntry? Episode);

/// <summary>
/// Counts down and then plays the next episode. The wait is cancelable from any input method, the
/// file is checked again at zero rather than trusted from when the countdown started, and the session
/// is opened through the coordinator, which never allows a second one.
/// </summary>
public sealed class StartNextEpisodeCountdown
{
    /// <summary>Where the chosen countdown length is stored between sessions.</summary>
    /// <remarks>
    /// The constants forward to <see cref="ContinuityCountdown"/>, which owns the wait since the
    /// course chain arrived (CRS-004). They stay declared here because the settings surface, T28's
    /// tests and the shortcut documentation all name them through this type.
    /// </remarks>
    public const string SettingKey = ContinuityCountdown.SettingKey;

    public const int DefaultCountdownSeconds = ContinuityCountdown.DefaultCountdownSeconds;

    public const int MaximumCountdownSeconds = ContinuityCountdown.MaximumCountdownSeconds;

    private readonly GetNextEpisode _next;
    private readonly IEpisodeSequenceRepository _repository;
    private readonly IPlaybackSessionCoordinator _coordinator;
    private readonly ContinuityCountdown _countdown;

    public StartNextEpisodeCountdown(
        GetNextEpisode next,
        IEpisodeSequenceRepository repository,
        IPlaybackSessionCoordinator coordinator,
        ISettingsStore settings,
        IClock clock)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _countdown = new ContinuityCountdown(settings, clock);
        _countdown.Ticked += (sender, remaining) => Ticked?.Invoke(this, remaining);
    }

    /// <summary>Raised once per second with the seconds still to go, ending at zero.</summary>
    public event EventHandler<int>? Ticked;

    /// <summary>The countdown in force, from zero — which switches the chain off — to sixty.</summary>
    public int CountdownSeconds => _countdown.CountdownSeconds;

    /// <summary>Stores a new countdown length, clamped to the accepted range.</summary>
    public void ConfigureCountdown(int seconds) => _countdown.ConfigureCountdown(seconds);

    /// <summary>Stops a countdown that is running; whoever calls it may be a key, a click, or a menu.</summary>
    public void Cancel() => _countdown.Cancel();

    public async Task<NextEpisodeResult> ExecuteAsync(
        TitleId showId,
        EpisodeId currentEpisode,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _next
            .ExecuteAsync(showId, currentEpisode, cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            return new NextEpisodeResult(NextEpisodeOutcome.NoNextEpisode, null);
        }

        var seconds = CountdownSeconds;
        if (seconds == 0)
        {
            return new NextEpisodeResult(NextEpisodeOutcome.Disabled, candidate);
        }

        if (!await _countdown.WaitAsync(seconds, cancellationToken).ConfigureAwait(false))
        {
            return new NextEpisodeResult(NextEpisodeOutcome.Cancelled, candidate);
        }

        // The drive may have been pulled out while the countdown ran, so availability is confirmed
        // now rather than trusted from when the offer was made.
        var confirmed = await _repository.GetAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
        if (confirmed is not { IsPlayable: true })
        {
            return new NextEpisodeResult(NextEpisodeOutcome.Unavailable, candidate);
        }

        try
        {
            _ = await _coordinator
                .StartAsync(
                    new PlaybackRequest(confirmed.MediaFileId!.Value, confirmed.Path!),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PlaybackFailureException)
        {
            return new NextEpisodeResult(NextEpisodeOutcome.Unavailable, confirmed);
        }

        return new NextEpisodeResult(NextEpisodeOutcome.Started, confirmed);
    }
}
