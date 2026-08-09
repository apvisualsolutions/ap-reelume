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
}
