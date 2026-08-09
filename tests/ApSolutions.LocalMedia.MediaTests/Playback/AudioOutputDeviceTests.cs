using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.MediaTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Routing the session's audio to a render endpoint, against a real engine decoding real media
/// (AUD-A01). The engine joins the catalog's Windows identifiers to LibVLC's own by their common
/// endpoint suffix, and an identifier nobody announces is a no-op — losing a device mid-session
/// must never kill the session.
/// </summary>
[Trait("Category", "RealMedia")]
public sealed class AudioOutputDeviceTests
{
    private const string Sample = "mkv-dual-audio-spanish-first";

    [Fact]
    public async Task Routing_audio_never_kills_a_real_playing_session()
    {
        var sample = MediaManifest.Require(Sample);
        var path = await CodecMatrixTests.RequireSampleAsync(sample);
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);
        await engine.OpenAsync(
            new PlaybackRequest(new MediaFileId(Guid.NewGuid()), path),
            TestContext.Current.CancellationToken);
        await engine.PlayAsync(TestContext.Current.CancellationToken);

        // An identifier the machine announces would match by suffix; one that nobody announces is
        // handed over unchanged and VLC ignores it. Either way the session has to survive.
        await engine.SetAudioOutputDeviceAsync(
            "{00000000-0000-0000-0000-00000000a0d1}",
            TestContext.Current.CancellationToken);

        var snapshot = await engine.GetSnapshotAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(PlaybackState.Failed, snapshot.State);
        await engine.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Routing_without_a_session_names_the_absence_instead_of_guessing()
    {
        await using var factory = LibVlcFactory.CreateHeadless();
        await using var engine = new LibVlcMediaPlayerEngine(factory);
        await engine.InitializeAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            engine.SetAudioOutputDeviceAsync("{irrelevant}", TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.EngineUnavailable, exception.Failure.Code);
    }
}
