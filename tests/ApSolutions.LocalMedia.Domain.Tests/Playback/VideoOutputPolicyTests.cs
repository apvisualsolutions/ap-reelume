using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Playback;

/// <summary>
/// What the engine reports about video output has to come from the real state of the source and the
/// display, and Dolby Vision has to stay explicitly unsupported rather than silently degraded.
/// </summary>
public sealed class VideoOutputPolicyTests
{
    private static readonly DisplayCapabilities HdrDisplay = new(SupportsHdr10: true, HdrEnabled: true);
    private static readonly DisplayCapabilities HdrCapableButOff = new(SupportsHdr10: true, HdrEnabled: false);
    private static readonly DisplayCapabilities SdrDisplay = new(SupportsHdr10: false, HdrEnabled: false);

    [Fact]
    public void An_HDR10_source_on_an_HDR_display_passes_through()
    {
        var decision = VideoOutputPolicy.Decide(
            new VideoSourceCapabilities(HdrFormat.Hdr10, 3840, 2160),
            HdrDisplay,
            hardwareRequested: true,
            hardwareAvailable: true);

        Assert.Equal(VideoOutputPath.Hdr10Passthrough, decision.Path);
        Assert.True(decision.HardwareAccelerationActive);
        Assert.False(decision.FellBackToSoftware);
        Assert.Null(decision.UnsupportedReason);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void An_HDR10_source_on_a_display_without_active_HDR_is_tone_mapped(bool supports, bool enabled)
    {
        var decision = VideoOutputPolicy.Decide(
            new VideoSourceCapabilities(HdrFormat.Hdr10, 3840, 2160),
            new DisplayCapabilities(supports, enabled),
            hardwareRequested: true,
            hardwareAvailable: true);

        Assert.Equal(VideoOutputPath.SdrToneMapped, decision.Path);
        Assert.Null(decision.UnsupportedReason);
    }

    [Fact]
    public void An_SDR_source_stays_SDR_on_any_display()
    {
        foreach (var display in new[] { HdrDisplay, HdrCapableButOff, SdrDisplay })
        {
            var decision = VideoOutputPolicy.Decide(
                new VideoSourceCapabilities(HdrFormat.None, 1920, 1080),
                display,
                hardwareRequested: false,
                hardwareAvailable: true);

            Assert.Equal(VideoOutputPath.Sdr, decision.Path);
            Assert.False(decision.HardwareAccelerationActive);
        }
    }

    [Fact]
    public void Failed_acceleration_falls_back_to_software_without_changing_the_output_path()
    {
        var decision = VideoOutputPolicy.Decide(
            new VideoSourceCapabilities(HdrFormat.Hdr10, 3840, 2160),
            HdrDisplay,
            hardwareRequested: true,
            hardwareAvailable: false);

        Assert.Equal(VideoOutputPath.Hdr10Passthrough, decision.Path);
        Assert.True(decision.HardwareAccelerationRequested);
        Assert.False(decision.HardwareAccelerationActive);
        Assert.True(decision.FellBackToSoftware);
        Assert.Null(decision.UnsupportedReason);
    }

    [Fact]
    public void Dolby_Vision_is_reported_unsupported_and_is_never_offered_as_a_path()
    {
        var decision = VideoOutputPolicy.Decide(
            new VideoSourceCapabilities(HdrFormat.DolbyVision, 3840, 2160),
            HdrDisplay,
            hardwareRequested: true,
            hardwareAvailable: true);

        Assert.Equal(PlaybackFailureCode.UnsupportedCapability, decision.UnsupportedReason);
        Assert.Equal(VideoOutputPath.SdrToneMapped, decision.Path);
        Assert.DoesNotContain(
            Enum.GetNames<VideoOutputPath>(),
            name => name.Contains("Dolby", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(VideoOutputPath.Hdr10Passthrough, decision.Path);
        Assert.Equal(
            [VideoOutputPath.Sdr, VideoOutputPath.Hdr10Passthrough, VideoOutputPath.SdrToneMapped],
            VideoOutputPolicy.SelectablePaths);
        Assert.Contains(
            PlaybackRecoveryAction.OpenExternally,
            PlaybackDiagnosticsPolicy.RecoveryActionsFor(decision.UnsupportedReason!.Value));
    }

    [Fact]
    public void The_indicator_describes_the_state_that_actually_happened()
    {
        var software = VideoOutputPolicy.Decide(
            new VideoSourceCapabilities(HdrFormat.None, 1280, 720),
            SdrDisplay,
            hardwareRequested: true,
            hardwareAvailable: false);

        Assert.True(software.FellBackToSoftware);
        Assert.False(software.HardwareAccelerationActive);
        Assert.Equal(VideoOutputPath.Sdr, software.Path);

        var capabilities = software.ToCapabilities();
        Assert.True(capabilities.HardwareAccelerationRequested);
        Assert.False(capabilities.HardwareAccelerationActive);
        Assert.Equal(HdrFormat.None, capabilities.SourceHdr);
        Assert.False(capabilities.DisplaySupportsHdr);
        Assert.Equal(VideoOutputPath.Sdr, capabilities.OutputPath);
    }

    [Fact]
    public void The_policy_rejects_a_missing_source_or_display()
    {
        Assert.Throws<ArgumentNullException>(() => VideoOutputPolicy.Decide(null!, SdrDisplay, true, true));
        Assert.Throws<ArgumentNullException>(() => VideoOutputPolicy.Decide(
            new VideoSourceCapabilities(HdrFormat.None, 1, 1),
            null!,
            true,
            true));
    }
}
