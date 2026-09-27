// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

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

    /// <summary>
    /// The film itself is HDR10, whatever path its picture takes. The badge names the source and the
    /// line under it names the path; until 2026-09-27 the badge followed the path, so an HDR10 film
    /// brought down to standard range was labelled «SDR».
    /// </summary>
    public bool IsHdrSource => _capabilities?.SourceHdr == HdrFormat.Hdr10;

    public bool IsHardwareAccelerated => _capabilities?.HardwareAccelerationActive == true;

    /// <summary>True when acceleration was asked for and the engine had to decode in software.</summary>
    public bool FellBackToSoftware => _fellBackToSoftware;

    /// <summary>True when the source declares a format this release does not implement.</summary>
    public bool IsUnsupportedFormat => _capabilities?.SourceHdr == HdrFormat.DolbyVision;

    /// <summary>
    /// Something worth stating about how this is being decoded — a fact, not a problem.
    /// </summary>
    /// <remarks>
    /// Which path the video took and whether the GPU is doing the work are things somebody may want to
    /// read; none of them is anything going wrong, and painting them in a warning box would say
    /// otherwise about a video that is playing perfectly.
    /// </remarks>
    public bool HasDecodeFacts =>
        IsHdrPassthrough || IsToneMapped || IsStandardDynamicRange || IsHardwareAccelerated;

    /// <summary>
    /// What is being played is not quite what was asked for, which is a warning and not a failure.
    /// </summary>
    /// <remarks>
    /// Falling back to software still plays; an unsupported HDR format still shows a picture. Neither
    /// is a failure — the failure surface is <c>PlayerView</c>'s and it says something else entirely.
    /// </remarks>
    public bool HasDecodeWarnings => FellBackToSoftware || IsUnsupportedFormat;

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
            nameof(IsHdrSource),
            nameof(IsHardwareAccelerated),
            nameof(FellBackToSoftware),
            nameof(IsUnsupportedFormat),
            nameof(HasDecodeFacts),
            nameof(HasDecodeWarnings),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
