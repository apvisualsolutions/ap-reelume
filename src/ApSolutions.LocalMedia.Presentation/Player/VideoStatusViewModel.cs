using System.ComponentModel;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Shows what the engine actually did with the picture: the output path, whether hardware decoding
/// is in force, and whether it had to fall back. Every flag mirrors a reported capability; none of
/// them is a promise made before playback.
/// </summary>
public sealed class VideoStatusViewModel : INotifyPropertyChanged
{
    private PlaybackCapabilities? _capabilities;
    private bool _fellBackToSoftware;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasStatus => _capabilities is not null;

    public bool IsHdrPassthrough => _capabilities?.OutputPath == VideoOutputPath.Hdr10Passthrough;

    public bool IsToneMapped => _capabilities?.OutputPath == VideoOutputPath.SdrToneMapped;

    public bool IsStandardDynamicRange => _capabilities?.OutputPath == VideoOutputPath.Sdr;

    public bool IsHardwareAccelerated => _capabilities?.HardwareAccelerationActive == true;

    /// <summary>True when acceleration was asked for and the engine had to decode in software.</summary>
    public bool FellBackToSoftware => _fellBackToSoftware;

    /// <summary>True when the source declares a format this release does not implement.</summary>
    public bool IsUnsupportedFormat => _capabilities?.SourceHdr == HdrFormat.DolbyVision;

    public bool DisplaySupportsHdr => _capabilities?.DisplaySupportsHdr == true;

    /// <summary>Applies what the engine reported; the view never computes this itself.</summary>
    public void Apply(PlaybackCapabilities? capabilities, bool fellBackToSoftware)
    {
        _capabilities = capabilities;
        _fellBackToSoftware = fellBackToSoftware;
        foreach (var name in new[]
        {
            nameof(HasStatus),
            nameof(IsHdrPassthrough),
            nameof(IsToneMapped),
            nameof(IsStandardDynamicRange),
            nameof(IsHardwareAccelerated),
            nameof(FellBackToSoftware),
            nameof(IsUnsupportedFormat),
            nameof(DisplaySupportsHdr),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
