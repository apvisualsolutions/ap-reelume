// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Common;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Continuity;

/// <summary>
/// The wait both continuity chains hold (PLY-011, CRS-004): its length, its announcement each
/// second, and the two ways it can end.
/// </summary>
/// <remarks>
/// Exercised directly rather than only through the two chains above it. Driven from them, the arms
/// that never got taken were the ones that only matter when nobody is listening or when the session
/// itself is going away — which is exactly where a shared piece has to be right, because the two
/// callers each assume the other proved it.
/// </remarks>
public sealed class ContinuityCountdownTests
{
    [Fact]
    public async Task The_wait_announces_every_second_and_then_zero()
    {
        var announced = new List<int>();
        var countdown = new ContinuityCountdown(new InMemorySettings(), new InstantClock());
        countdown.Ticked += (_, remaining) => announced.Add(remaining);

        var completed = await countdown.WaitAsync(3, TestContext.Current.CancellationToken);

        Assert.True(completed);
        Assert.Equal([3, 2, 1, 0], announced);
    }

    /// <summary>
    /// Nobody subscribed, which is the arm the two chains never take because both attach a handler
    /// in their constructor. A null-conditional invoke is still a branch, and a shared piece that
    /// threw here would take the session with it.
    /// </summary>
    [Fact]
    public async Task A_wait_nobody_is_listening_to_still_runs_to_the_end()
    {
        var countdown = new ContinuityCountdown(new InMemorySettings(), new InstantClock());

        Assert.True(await countdown.WaitAsync(2, TestContext.Current.CancellationToken));
    }

    /// <summary>Zero seconds announces the end and nothing else; there is no second to count.</summary>
    [Fact]
    public async Task A_wait_of_zero_seconds_announces_only_the_end()
    {
        var announced = new List<int>();
        var countdown = new ContinuityCountdown(new InMemorySettings(), new InstantClock());
        countdown.Ticked += (_, remaining) => announced.Add(remaining);

        Assert.True(await countdown.WaitAsync(0, TestContext.Current.CancellationToken));
        Assert.Equal([0], announced);
    }

    [Fact]
    public async Task Cancelling_ends_the_wait_and_says_it_did_not_finish()
    {
        var countdown = new ContinuityCountdown(new InMemorySettings(), new InstantClock());
        var clock = new InstantClock();
        var stopping = new ContinuityCountdown(new InMemorySettings(), clock);
        clock.OnDelay = stopping.Cancel;

        Assert.False(await stopping.WaitAsync(5, TestContext.Current.CancellationToken));
        Assert.True(await countdown.WaitAsync(1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The session going away is <b>not</b> the same as somebody saying no, and the difference is
    /// what stops a closing application from being recorded as a choice: an external cancellation
    /// comes back as the exception rather than as «cancelled».
    /// </summary>
    [Fact]
    public async Task The_session_going_away_is_not_recorded_as_somebody_cancelling()
    {
        using var session = new CancellationTokenSource();
        var clock = new InstantClock { OnDelay = session.Cancel };
        var countdown = new ContinuityCountdown(new InMemorySettings(), clock);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => countdown.WaitAsync(5, session.Token));
    }

    /// <summary>Cancelling when nothing is running is a no-op, not a failure.</summary>
    [Fact]
    public void Cancelling_a_wait_that_is_not_running_does_nothing()
    {
        new ContinuityCountdown(new InMemorySettings(), new InstantClock()).Cancel();
    }

    /// <summary>The default applies until somebody has stored a length of their own.</summary>
    [Fact]
    public void An_unconfigured_countdown_takes_the_default()
    {
        var countdown = new ContinuityCountdown(new InMemorySettings(), new InstantClock());

        Assert.Equal(ContinuityCountdown.DefaultCountdownSeconds, countdown.CountdownSeconds);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(25, 25)]
    [InlineData(600, ContinuityCountdown.MaximumCountdownSeconds)]
    public void A_stored_length_is_clamped_to_the_accepted_range(int written, int expected)
    {
        var settings = new InMemorySettings();
        var countdown = new ContinuityCountdown(settings, new InstantClock());

        countdown.ConfigureCountdown(written);

        Assert.Equal(expected, countdown.CountdownSeconds);
        // Read back through a second instance: the length survives the object, which is the whole
        // point of it living in the settings store.
        Assert.Equal(expected, new ContinuityCountdown(settings, new InstantClock()).CountdownSeconds);
    }

    [Fact]
    public void Both_dependencies_are_required()
    {
        Assert.Throws<ArgumentNullException>(() => new ContinuityCountdown(null!, new InstantClock()));
        Assert.Throws<ArgumentNullException>(() => new ContinuityCountdown(new InMemorySettings(), null!));
    }

    private sealed class InMemorySettings : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }

    /// <summary>A clock that returns at once, running whatever was hooked to the first delay.</summary>
    private sealed class InstantClock : IClock
    {
        private bool _fired;

        public Action? OnDelay { get; set; }

        public DateTimeOffset UtcNow { get; } = new(2026, 9, 1, 20, 0, 0, TimeSpan.Zero);

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            if (!_fired && OnDelay is { } hook)
            {
                _fired = true;
                hook();
            }

            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask;
        }
    }
}
