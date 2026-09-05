// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The Playback section: the toggle owns whether the next thing starts on its own, the slider owns
/// how long the wait is, and between the two they must never disagree about one stored number.
/// </summary>
public sealed class PlaybackSettingsTests
{
    private static PlaybackSettingsViewModel Build(int initial, out Func<int> read)
    {
        var stored = initial;
        read = () => stored;
        return new PlaybackSettingsViewModel(() => stored, value => stored = value);
    }

    [Fact]
    public void A_stored_length_reads_as_on_with_that_length()
    {
        var viewModel = Build(30, out _);

        Assert.True(viewModel.IsCountdownEnabled);
        Assert.Equal(30, viewModel.CountdownSeconds);
    }

    [Fact]
    public void A_stored_zero_reads_as_off()
    {
        var viewModel = Build(0, out _);

        Assert.False(viewModel.IsCountdownEnabled);
    }

    [Fact]
    public void Switching_off_writes_the_zero_the_chain_reads()
    {
        var viewModel = Build(30, out var read);

        viewModel.IsCountdownEnabled = false;

        Assert.Equal(0, read());
    }

    /// <summary>
    /// The whole reason the toggle and the slider are two rows over one number: switching off and
    /// back on must not quietly discard a chosen length. A person who set thirty seconds and turned
    /// the chain off for one evening gets thirty back, not the default ten.
    /// </summary>
    [Fact]
    public void Switching_back_on_restores_the_length_that_was_in_force()
    {
        var viewModel = Build(30, out var read);

        viewModel.IsCountdownEnabled = false;
        viewModel.IsCountdownEnabled = true;

        Assert.Equal(30, read());
        Assert.Equal(30, viewModel.CountdownSeconds);
    }

    [Fact]
    public void Switching_on_from_a_stored_zero_takes_the_default()
    {
        var viewModel = Build(0, out var read);

        viewModel.IsCountdownEnabled = true;

        Assert.Equal(PlaybackSettingsViewModel.DefaultCountdownSeconds, read());
    }

    [Fact]
    public void The_length_persists_the_moment_it_changes_and_announces_it()
    {
        var viewModel = Build(10, out var read);
        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        viewModel.CountdownSeconds = 45;

        Assert.Equal(45, read());
        Assert.Contains(nameof(PlaybackSettingsViewModel.CountdownSeconds), announced);
    }

    /// <summary>
    /// Zero is the toggle's word alone. A slider that could write it would give two controls the same
    /// say over one value, and dragging to the left edge would switch the chain off while the toggle
    /// above still read «on» — a state the person never asked for and cannot see.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1)]
    public void The_slider_never_writes_a_length_the_chain_would_read_as_off(int asked)
    {
        var viewModel = Build(10, out var read);

        viewModel.CountdownSeconds = asked;

        Assert.True(read() >= PlaybackSettingsViewModel.MinimumSeconds);
        Assert.True(viewModel.IsCountdownEnabled);
    }

    [Fact]
    public void A_length_past_the_ceiling_is_clamped_rather_than_stored()
    {
        var viewModel = Build(10, out var read);

        viewModel.CountdownSeconds = 600;

        Assert.Equal(PlaybackSettingsViewModel.MaximumSeconds, read());
    }

    /// <summary>
    /// Switching off announces the length as well as the switch, because the row beneath disappears
    /// with it and a view that only heard about the switch would leave a stale number behind.
    /// </summary>
    [Fact]
    public void Switching_the_countdown_announces_both_rows()
    {
        var viewModel = Build(20, out _);
        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        viewModel.IsCountdownEnabled = false;

        Assert.Contains(nameof(PlaybackSettingsViewModel.IsCountdownEnabled), announced);
        Assert.Contains(nameof(PlaybackSettingsViewModel.CountdownSeconds), announced);
    }

    [Fact]
    public void Setting_the_same_value_twice_writes_once()
    {
        var writes = 0;
        var stored = 20;
        var viewModel = new PlaybackSettingsViewModel(() => stored, value => { stored = value; writes++; });

        viewModel.CountdownSeconds = 20;
        viewModel.IsCountdownEnabled = true;

        Assert.Equal(0, writes);
    }

    /// <summary>
    /// The slider is hidden while the countdown is off, so a write can only arrive from a binding
    /// settling as the row disappears. It must be remembered and must not switch the chain back on:
    /// a control going away is not somebody asking for the next episode to play itself.
    /// </summary>
    [Fact]
    public void A_length_written_while_the_countdown_is_off_is_remembered_without_switching_it_on()
    {
        var viewModel = Build(0, out var read);
        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        viewModel.CountdownSeconds = 40;

        Assert.Equal(0, read());
        Assert.False(viewModel.IsCountdownEnabled);
        Assert.Contains(nameof(PlaybackSettingsViewModel.CountdownSeconds), announced);

        // And it is the length that comes back, which is the whole point of remembering it.
        viewModel.IsCountdownEnabled = true;
        Assert.Equal(40, read());
    }

    /// <summary>
    /// Reading the length while the countdown is off answers the floor rather than the stored zero,
    /// so the slider never shows a value it could not produce if the row came back.
    /// </summary>
    [Fact]
    public void The_length_read_while_off_is_never_the_stored_zero()
    {
        var viewModel = Build(0, out _);

        Assert.False(viewModel.IsCountdownEnabled);
        Assert.True(viewModel.CountdownSeconds >= PlaybackSettingsViewModel.MinimumSeconds);
    }

    /// <summary>
    /// The slider's own bounds, which the view binds to by static reference. They are doubles because
    /// a <c>RangeBase</c> takes doubles, and asserting them here is what makes that a decision rather
    /// than a compiler accident nobody would notice changing.
    /// </summary>
    [Fact]
    public void The_slider_bounds_are_the_seconds_the_store_accepts()
    {
        Assert.Equal(PlaybackSettingsViewModel.MinimumSeconds, PlaybackSettingsViewModel.MinimumCountdownSeconds);
        Assert.Equal(PlaybackSettingsViewModel.MaximumSeconds, PlaybackSettingsViewModel.MaximumCountdownSeconds);
        Assert.True(PlaybackSettingsViewModel.MinimumCountdownSeconds > 0);
    }

    [Fact]
    public void The_section_refuses_to_exist_half_armed()
    {
        Assert.Throws<ArgumentNullException>(() => new PlaybackSettingsViewModel(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new PlaybackSettingsViewModel(() => 0, null!));
    }
}
