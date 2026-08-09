namespace ApSolutions.LocalMedia.Domain.Playback;

/// <summary>
/// Lists the audio outputs the machine currently offers. Asked again whenever the interface opens or
/// a device appears or disappears, so a headset that was unplugged stops being selectable.
/// </summary>
public interface IAudioDeviceCatalog
{
    Task<IReadOnlyList<AudioOutputDevice>> GetOutputsAsync(CancellationToken cancellationToken = default);
}
