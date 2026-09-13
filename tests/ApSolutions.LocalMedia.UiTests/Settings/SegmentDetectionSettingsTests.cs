// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using ApSolutions.LocalMedia.Presentation.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The detection switch: off until a person turns it on, persisted the moment it changes, and
/// announced so the view can follow.
/// </summary>
public sealed class SegmentDetectionSettingsTests
{
    [Fact]
    public void Detection_is_off_by_default()
    {
        var stored = false;
        var viewModel = new SegmentDetectionSettingsViewModel(() => stored, value => stored = value);

        Assert.False(viewModel.IsEnabled);
    }

    [Fact]
    public void Turning_the_switch_on_persists_and_announces_the_change()
    {
        var stored = false;
        var viewModel = new SegmentDetectionSettingsViewModel(() => stored, value => stored = value);
        var announced = new List<string?>();
        viewModel.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        viewModel.IsEnabled = true;

        Assert.True(stored);
        Assert.True(viewModel.IsEnabled);
        Assert.Contains(nameof(SegmentDetectionSettingsViewModel.IsEnabled), announced);
    }

    [Fact]
    public void The_switch_refuses_to_exist_half_armed()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => new SegmentDetectionSettingsViewModel(null!, _ => { }));
        _ = Assert.Throws<ArgumentNullException>(
            () => new SegmentDetectionSettingsViewModel(() => false, null!));
    }

    [Fact]
    public void Setting_the_switch_to_what_it_already_is_writes_and_announces_nothing()
    {
        var stored = true;
        var writes = 0;
        var viewModel = new SegmentDetectionSettingsViewModel(
            () => stored,
            value =>
            {
                stored = value;
                writes++;
            });
        var announced = 0;
        viewModel.PropertyChanged += (_, _) => announced++;

        viewModel.IsEnabled = true;

        Assert.Equal(0, writes);
        Assert.Equal(0, announced);
    }

    [Fact]
    public void Turning_the_switch_off_persists_false()
    {
        var stored = true;
        var viewModel = new SegmentDetectionSettingsViewModel(() => stored, value => stored = value);

        viewModel.IsEnabled = false;

        Assert.False(stored);
        Assert.False(viewModel.IsEnabled);
    }

    /// <summary>
    /// UX-010, and it is asserted on what was stored rather than on the property: a reset that only
    /// moved the view model would leave the switch on across a restart while looking restored.
    /// </summary>
    [Fact]
    public void Restoring_the_defaults_stores_the_factory_value_rather_than_only_showing_it()
    {
        var stored = true;
        var viewModel = new SegmentDetectionSettingsViewModel(() => stored, value => stored = value);

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(SegmentDetectionSettingsViewModel.DefaultEnabled, stored);
        Assert.False(stored);
        Assert.False(viewModel.IsEnabled);
    }

    [Fact]
    public void Restoring_what_is_already_the_default_writes_nothing()
    {
        var stored = false;
        var writes = 0;
        var viewModel = new SegmentDetectionSettingsViewModel(
            () => stored,
            value =>
            {
                stored = value;
                writes++;
            });

        viewModel.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(0, writes);
    }

    [Fact]
    public void The_restore_is_offered_whether_or_not_there_is_anything_to_undo()
    {
        var viewModel = new SegmentDetectionSettingsViewModel(() => false, _ => { });

        Assert.True(viewModel.RestoreDefaultsCommand.CanExecute(null));
    }
}
