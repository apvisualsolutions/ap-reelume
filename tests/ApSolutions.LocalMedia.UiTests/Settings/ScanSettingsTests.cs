// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Presentation.Settings;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The scanning group: whether local roots are watched, and how long the fallback sweep waits.
/// </summary>
/// <remarks>
/// <b>These two values govern nothing today.</b> They are fields on this view model with no store
/// behind them and no reader anywhere in <c>src/</c> — the house defect, registered and never fed —
/// and that is a separate row rather than a reason to leave the group out of UX-010's closed list.
/// Out of the list, the day somebody wires them up a group is born that the gate cannot see.
/// </remarks>
public sealed class ScanSettingsTests
{
    [Fact]
    public void The_group_starts_at_its_factory_values()
    {
        var viewModel = new ScanSettingsViewModel();

        Assert.Equal(ScanSettingsViewModel.DefaultWatchLocalRoots, viewModel.WatchLocalRoots);
        Assert.Equal(ScanSettingsViewModel.DefaultFallbackIntervalMinutes, viewModel.FallbackIntervalMinutes);
    }

    /// <summary>
    /// UX-010, and both halves at once: restoring one value and leaving the other is what a reset
    /// written per control looks like from the outside, and it is exactly what the matrix forbids.
    /// </summary>
    [Fact]
    public void Restoring_the_defaults_puts_back_every_value_in_the_group()
    {
        var viewModel = new ScanSettingsViewModel
        {
            WatchLocalRoots = false,
            FallbackIntervalMinutes = 240,
        };

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(ScanSettingsViewModel.DefaultWatchLocalRoots, viewModel.WatchLocalRoots);
        Assert.Equal(ScanSettingsViewModel.DefaultFallbackIntervalMinutes, viewModel.FallbackIntervalMinutes);
    }

    [Fact]
    public void Restoring_announces_both_values_so_the_view_follows()
    {
        var viewModel = new ScanSettingsViewModel
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

    [Fact]
    public void Restoring_what_is_already_the_default_announces_nothing()
    {
        var viewModel = new ScanSettingsViewModel();
        var announced = 0;
        viewModel.PropertyChanged += (_, _) => announced++;

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(0, announced);
    }

    [Fact]
    public void The_restore_is_always_offered()
    {
        var viewModel = new ScanSettingsViewModel();

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
        var viewModel = new ScanSettingsViewModel();
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
