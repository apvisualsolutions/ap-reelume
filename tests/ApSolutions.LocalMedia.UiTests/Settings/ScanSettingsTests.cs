// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.TestSupport;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The scanning group: whether local roots are watched, and how long the fallback sweep waits.
/// </summary>
/// <remarks>
/// <b>These two values governed nothing until ENG-044 closed, and this file's older version said so
/// in its own notes.</b> They were fields on this view model with no store behind them and no reader
/// anywhere in <c>src/</c> — the house defect, registered and never fed. So the assertions changed
/// shape: what is measured now is the store, not a field, because a field that remembers its own
/// value is indistinguishable from one the application actually reads.
/// </remarks>
public sealed class ScanSettingsTests
{
    [Fact]
    public void The_group_reads_what_the_store_already_holds()
    {
        var settings = new InMemoryScanWatchSettings(watchLocalRoots: false, TimeSpan.FromMinutes(90));

        var viewModel = new ScanSettingsViewModel(settings);

        Assert.False(viewModel.WatchLocalRoots);
        Assert.Equal(90, viewModel.FallbackIntervalMinutes);
    }

    [Fact]
    public void The_group_starts_at_its_factory_values_when_nobody_has_answered()
    {
        var viewModel = new ScanSettingsViewModel(new InMemoryScanWatchSettings());

        Assert.Equal(ScanSettingsViewModel.DefaultWatchLocalRoots, viewModel.WatchLocalRoots);
        Assert.Equal(ScanSettingsViewModel.DefaultFallbackIntervalMinutes, viewModel.FallbackIntervalMinutes);
    }

    /// <summary>
    /// The factory interval is the one the code ships with rather than a second number painted next
    /// to it: the screen used to say thirty minutes while the sweep ran every fifteen.
    /// </summary>
    [Fact]
    public void The_factory_interval_is_the_one_the_code_ships_with()
    {
        Assert.Equal(
            (int)ScanWatchPolicy.DefaultSweepInterval.TotalMinutes,
            ScanSettingsViewModel.DefaultFallbackIntervalMinutes);
    }

    [Fact]
    public void Unticking_the_box_reaches_the_store()
    {
        var settings = new InMemoryScanWatchSettings();
        var viewModel = new ScanSettingsViewModel(settings);

        viewModel.WatchLocalRoots = false;

        Assert.False(settings.WatchLocalRoots);
    }

    [Fact]
    public void Changing_the_interval_reaches_the_store()
    {
        var settings = new InMemoryScanWatchSettings();
        var viewModel = new ScanSettingsViewModel(settings);

        viewModel.FallbackIntervalMinutes = 45;

        Assert.Equal(TimeSpan.FromMinutes(45), settings.SweepInterval);
    }

    /// <summary>
    /// UX-010, and both halves at once: restoring one value and leaving the other is what a reset
    /// written per control looks like from the outside, and it is exactly what the matrix forbids.
    /// </summary>
    [Fact]
    public void Restoring_the_defaults_puts_back_every_value_in_the_group()
    {
        var settings = new InMemoryScanWatchSettings();
        var viewModel = new ScanSettingsViewModel(settings)
        {
            WatchLocalRoots = false,
            FallbackIntervalMinutes = 240,
        };

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(ScanSettingsViewModel.DefaultWatchLocalRoots, viewModel.WatchLocalRoots);
        Assert.Equal(ScanSettingsViewModel.DefaultFallbackIntervalMinutes, viewModel.FallbackIntervalMinutes);
        Assert.True(settings.WatchLocalRoots);
        Assert.Equal(ScanWatchPolicy.DefaultSweepInterval, settings.SweepInterval);
    }

    [Fact]
    public void Restoring_announces_both_values_so_the_view_follows()
    {
        var viewModel = new ScanSettingsViewModel(new InMemoryScanWatchSettings())
        {
            WatchLocalRoots = false,
            FallbackIntervalMinutes = 240,
        };
        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Contains(nameof(ScanSettingsViewModel.WatchLocalRoots), announced);
        Assert.Contains(nameof(ScanSettingsViewModel.FallbackIntervalMinutes), announced);
    }

    /// <summary>
    /// A value written back over the one it already holds would make the watching restart for
    /// nothing, so the guard is not decoration: every write now cancels and rebuilds every watcher.
    /// </summary>
    [Fact]
    public void Setting_a_value_the_store_already_holds_writes_nothing_and_announces_nothing()
    {
        var settings = new InMemoryScanWatchSettings();
        var viewModel = new ScanSettingsViewModel(settings);
        var announced = 0;
        viewModel.PropertyChanged += (_, _) => announced++;

        viewModel.WatchLocalRoots = ScanSettingsViewModel.DefaultWatchLocalRoots;
        viewModel.FallbackIntervalMinutes = ScanSettingsViewModel.DefaultFallbackIntervalMinutes;
        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(0, announced);
        Assert.Equal(0, settings.Writes);
    }

    [Fact]
    public void The_screen_needs_somewhere_to_write() =>
        Assert.Throws<ArgumentNullException>(() => new ScanSettingsViewModel(null!));

    [Fact]
    public void The_restore_is_always_offered()
    {
        var viewModel = new ScanSettingsViewModel(new InMemoryScanWatchSettings());

        Assert.True(viewModel.RestoreDefaultsCommand.CanExecute(null));
    }

    /// <summary>
    /// Subscribing to a command that can never refuse is what a binding does on its own, and it has
    /// to be harmless: an <c>ICommand</c> whose availability never changes raises nothing, and a
    /// subscription that threw would take the view down at load rather than at the click.
    /// </summary>
    [Fact]
    public void Listening_for_the_restore_becoming_unavailable_is_harmless_because_it_never_does()
    {
        var viewModel = new ScanSettingsViewModel(new InMemoryScanWatchSettings());
        var raised = 0;
        void OnChanged(object? sender, EventArgs args) => raised++;

        viewModel.RestoreDefaultsCommand.CanExecuteChanged += OnChanged;
        viewModel.WatchLocalRoots = false;
        viewModel.RestoreDefaultsCommand.Execute(null);
        viewModel.RestoreDefaultsCommand.CanExecuteChanged -= OnChanged;

        Assert.Equal(0, raised);
        Assert.True(viewModel.RestoreDefaultsCommand.CanExecute(null));
    }

}
