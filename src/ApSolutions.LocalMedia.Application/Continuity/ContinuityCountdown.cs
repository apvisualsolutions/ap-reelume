// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Common;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// The wait itself: the configured length, the announcement each second, and the cancellation that
/// any input can reach.
/// </summary>
/// <remarks>
/// Extracted on 2026-09-01 when the course chain arrived (CRS-004), whose ficha says the countdown
/// «is PLY-011's». Two classes running their own loop would have been two countdowns that agree
/// today, and the one that stops agreeing is the one nobody is looking at — this repository's own
/// recurring defect, found six times over in a list of containers written six times.
/// <para>
/// <b>The setting key is shared and stays the episode one.</b> A person configures «how long before
/// the next thing starts», not one answer for series and another for courses, and the stored key is
/// what T28 wrote and what the settings surface already reads and writes. Renaming it would leave
/// every existing installation's chosen length behind on the old key, silently back at ten seconds.
/// </para>
/// </remarks>
public sealed class ContinuityCountdown
{
    /// <summary>Where the chosen countdown length is stored between sessions.</summary>
    public const string SettingKey = "continuity.next-episode-countdown-seconds";

    public const int DefaultCountdownSeconds = 10;

    public const int MaximumCountdownSeconds = 60;

    private readonly ISettingsStore _settings;
    private readonly IClock _clock;
    private CancellationTokenSource? _countdown;

    public ContinuityCountdown(ISettingsStore settings, IClock clock)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>Raised once per second with the seconds still to go, ending at zero.</summary>
    public event EventHandler<int>? Ticked;

    /// <summary>The countdown in force, from zero — which switches the chain off — to sixty.</summary>
    public int CountdownSeconds => Clamp(_settings.Read<int?>(SettingKey) ?? DefaultCountdownSeconds);

    /// <summary>Stores a new countdown length, clamped to the accepted range.</summary>
    public void ConfigureCountdown(int seconds) => _settings.Write(SettingKey, Clamp(seconds));

    /// <summary>Stops a countdown that is running; whoever calls it may be a key, a click, or a menu.</summary>
    public void Cancel() => _countdown?.Cancel();

    /// <summary>
    /// Counts <paramref name="seconds"/> down to zero. True when it ran out, false when something
    /// stopped it.
    /// </summary>
    /// <remarks>
    /// A cancellation asked for by <paramref name="cancellationToken"/> is <b>not</b> the same as one
    /// asked for by <see cref="Cancel"/> and does not come back as false: the first is the session
    /// going away and the second is a person saying no. Telling them apart is what stops a closing
    /// application from being recorded as somebody's choice.
    /// </remarks>
    public async Task<bool> WaitAsync(int seconds, CancellationToken cancellationToken = default)
    {
        using var countdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _countdown = countdown;
        try
        {
            for (var remaining = seconds; remaining > 0; remaining--)
            {
                Ticked?.Invoke(this, remaining);
                await _clock.DelayAsync(TimeSpan.FromSeconds(1), countdown.Token).ConfigureAwait(false);
            }

            Ticked?.Invoke(this, 0);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            _countdown = null;
        }
    }

    private static int Clamp(int seconds) => Math.Clamp(seconds, 0, MaximumCountdownSeconds);
}
