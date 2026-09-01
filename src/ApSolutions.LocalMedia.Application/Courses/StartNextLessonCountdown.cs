// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Application.Courses;

/// <summary>What happened and which lesson it was about.</summary>
/// <remarks>
/// The outcome enumeration is <see cref="NextEpisodeOutcome"/> and not one of its own, which is the
/// ficha's «la cuenta atrás es la de PLY-011» taken literally: the five ways a chain can end are the
/// same five, and a second enumeration naming them again is two lists that start agreeing and stop.
/// <see cref="NextEpisodeOutcome.NoNextEpisode"/> is what «Curso terminado» is drawn from.
/// </remarks>
public sealed record NextLessonResult(NextEpisodeOutcome Outcome, CourseLessonProgress? Lesson);

/// <summary>
/// Counts down and then plays the next lesson of the course (CRS-004).
/// </summary>
/// <remarks>
/// The wait, the configured length and the cancellation are <see cref="ContinuityCountdown"/>'s —
/// the very object PLY-011 uses, not a copy of its behaviour — so the length a person chose applies
/// to both chains and there is one loop to keep correct. What is this class's own is the two ends:
/// which lesson comes next, and confirming at zero that its file is still there.
/// <para>
/// <b>The revalidation is a re-read and not a recheck of what was held.</b> The course is read again
/// from the store when the countdown ends, so a drive pulled out — or a folder unmarked — during the
/// wait is found now rather than trusted from when the offer was made. That is the half of T28 that
/// a copy would most easily have left out, because it looks redundant right up until it is not.
/// </para>
/// </remarks>
public sealed class StartNextLessonCountdown
{
    private readonly GetLessonSession _sessions;
    private readonly IMediaFileRepository _files;
    private readonly IPlaybackSessionCoordinator _coordinator;
    private readonly ContinuityCountdown _countdown;

    public StartNextLessonCountdown(
        GetLessonSession sessions,
        IMediaFileRepository files,
        IPlaybackSessionCoordinator coordinator,
        ISettingsStore settings,
        IClock clock)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _countdown = new ContinuityCountdown(settings, clock);
        _countdown.Ticked += (sender, remaining) => Ticked?.Invoke(this, remaining);
    }

    /// <summary>Raised once per second with the seconds still to go, ending at zero.</summary>
    public event EventHandler<int>? Ticked;

    /// <summary>The countdown in force, from zero — which switches the chain off — to sixty.</summary>
    public int CountdownSeconds => _countdown.CountdownSeconds;

    /// <summary>Stops a countdown that is running; whoever calls it may be a key, a click, or a menu.</summary>
    public void Cancel() => _countdown.Cancel();

    /// <summary>
    /// The lesson that would follow the one this file backs, without starting anything. It is what
    /// the overlay names before the wait begins.
    /// </summary>
    public async Task<CourseLessonProgress?> PeekAsync(
        MediaFileId currentFile,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessions.FindAsync(currentFile, cancellationToken).ConfigureAwait(false);
        return session is null ? null : NextLessonPolicy.FindNext(session.Lessons, session.LessonId);
    }

    public async Task<NextLessonResult> ExecuteAsync(
        MediaFileId currentFile,
        CancellationToken cancellationToken = default)
    {
        var candidate = await PeekAsync(currentFile, cancellationToken).ConfigureAwait(false);
        if (candidate is null)
        {
            return new NextLessonResult(NextEpisodeOutcome.NoNextEpisode, null);
        }

        var seconds = CountdownSeconds;
        if (seconds == 0)
        {
            return new NextLessonResult(NextEpisodeOutcome.Disabled, candidate);
        }

        if (!await _countdown.WaitAsync(seconds, cancellationToken).ConfigureAwait(false))
        {
            return new NextLessonResult(NextEpisodeOutcome.Cancelled, candidate);
        }

        // Confirmed now rather than trusted from when the offer was made, which is T28's own rule.
        // The lesson row carries LIB-009's identity; what has to exist at zero is the file behind it.
        var confirmed = candidate.MediaFileId is { } fileId
            ? await _files.FindByIdAsync(fileId, cancellationToken).ConfigureAwait(false)
            : null;
        if (confirmed is null || string.IsNullOrWhiteSpace(confirmed.Path))
        {
            return new NextLessonResult(NextEpisodeOutcome.Unavailable, candidate);
        }

        try
        {
            _ = await _coordinator
                .StartAsync(new PlaybackRequest(confirmed.Id, confirmed.Path), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (PlaybackFailureException)
        {
            return new NextLessonResult(NextEpisodeOutcome.Unavailable, candidate);
        }

        return new NextLessonResult(NextEpisodeOutcome.Started, candidate);
    }
}
