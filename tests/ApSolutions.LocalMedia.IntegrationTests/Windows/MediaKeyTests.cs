using ApSolutions.LocalMedia.Application.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Windows;

/// <summary>
/// The routing side of media keys, driven by a fake source. This project targets the neutral
/// framework and cannot reference the Windows host, so the real service is asserted from the
/// accessibility suite; what is verified here is that a key produces exactly one action and that the
/// source is released when the session ends.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MediaKeyTests
{
    [Fact]
    public async Task A_media_key_produces_exactly_one_action()
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter((command, _) =>
        {
            executed.Add(command);
            return Task.CompletedTask;
        });
        var source = new FakeMediaKeySource();
        source.CommandReceived += async (_, command) =>
            await router.DispatchAsync(command, InputOrigin.MediaKey, CancellationToken.None);
        await source.StartAsync(TestContext.Current.CancellationToken);

        await source.RaiseAsync(PlaybackInputCommand.PlayPause);

        Assert.True(source.IsListening);
        Assert.Equal([PlaybackInputCommand.PlayPause], executed);
    }

    [Fact]
    public async Task A_key_and_a_keyboard_shortcut_for_the_same_action_do_not_stack()
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter((command, _) =>
        {
            executed.Add(command);
            return Task.CompletedTask;
        });

        _ = await router.DispatchAsync(
            PlaybackInputCommand.PlayPause,
            InputOrigin.MediaKey,
            TestContext.Current.CancellationToken);
        _ = await router.DispatchAsync(
            PlaybackInputCommand.PlayPause,
            InputOrigin.Keyboard,
            TestContext.Current.CancellationToken);

        Assert.Single(executed);
        Assert.Contains(InputOrigin.Keyboard, router.Suppressed);
    }

    [Fact]
    public async Task Every_key_the_service_claims_maps_to_a_transport_action()
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter(
            (command, _) =>
            {
                executed.Add(command);
                return Task.CompletedTask;
            },
            TimeSpan.Zero);
        var source = new FakeMediaKeySource();
        source.CommandReceived += async (_, command) =>
            await router.DispatchAsync(command, InputOrigin.MediaKey, CancellationToken.None);
        await source.StartAsync(TestContext.Current.CancellationToken);

        foreach (var command in new[]
        {
            PlaybackInputCommand.PlayPause,
            PlaybackInputCommand.Stop,
            PlaybackInputCommand.SkipForward,
            PlaybackInputCommand.SkipBackward,
        })
        {
            await source.RaiseAsync(command);
        }

        Assert.Equal(
            [
                PlaybackInputCommand.PlayPause,
                PlaybackInputCommand.Stop,
                PlaybackInputCommand.SkipForward,
                PlaybackInputCommand.SkipBackward,
            ],
            executed);
    }

    [Fact]
    public async Task Stopping_the_session_releases_the_source_and_no_further_key_acts()
    {
        var executed = new List<PlaybackInputCommand>();
        using var router = new InputCommandRouter(
            (command, _) =>
            {
                executed.Add(command);
                return Task.CompletedTask;
            },
            TimeSpan.Zero);
        var source = new FakeMediaKeySource();
        source.CommandReceived += async (_, command) =>
            await router.DispatchAsync(command, InputOrigin.MediaKey, CancellationToken.None);

        await source.StartAsync(TestContext.Current.CancellationToken);
        await source.RaiseAsync(PlaybackInputCommand.PlayPause);
        await source.StopAsync(TestContext.Current.CancellationToken);
        await source.RaiseAsync(PlaybackInputCommand.PlayPause);

        Assert.False(source.IsListening);
        Assert.Single(executed);
    }

    private sealed class FakeMediaKeySource : IMediaKeySource
    {
        public event EventHandler<PlaybackInputCommand>? CommandReceived;

        public bool IsListening { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            IsListening = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsListening = false;
            return Task.CompletedTask;
        }

        /// <summary>Delivers a key exactly as the real service would, and only while listening.</summary>
        public Task RaiseAsync(PlaybackInputCommand command)
        {
            if (IsListening)
            {
                CommandReceived?.Invoke(this, command);
            }

            return Task.CompletedTask;
        }
    }
}
