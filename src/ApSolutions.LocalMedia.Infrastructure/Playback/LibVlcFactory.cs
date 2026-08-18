// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Owns the native LibVLC instance. The instance is created once per option set and kept for the
/// lifetime of the process: repeatedly building and tearing down LibVLC is the native failure mode
/// already observed while probing media, so only media players and media objects are released here.
/// </summary>
public sealed class LibVlcFactory : IAsyncDisposable
{
    private static readonly string[] BaseOptions =
    [
        "--no-metadata-network-access",
        "--no-sub-autodetect-file",
        "--no-video-title-show",
    ];

    // The LibVLC "dummy" video output crashes the process while media are opened and released in
    // sequence, so headless runs keep a real decoder and discard frames through video callbacks.
    private static readonly string[] HeadlessOptions = [.. BaseOptions, "--aout=dummy"];
    private static readonly Lock InstanceSync = new();
    private static readonly Dictionary<string, LibVLC> SharedInstances = new(StringComparer.Ordinal);

    // Releasing a media the instant its player lets go is the native failure mode this repository
    // keeps relearning; a media rests for this window before its handle is disposed.
    private static readonly TimeSpan MediaQuiescence = TimeSpan.FromSeconds(1);

    // How often a flush looks at the queue. The drain does the releasing; this only watches it empty.
    private static readonly TimeSpan FlushPollInterval = TimeSpan.FromMilliseconds(25);
    private static readonly Lock ReleaseSync = new();
    private static readonly Queue<DeferredMedia> DeferredReleases = new();
    private static bool isDrainScheduled;

    private readonly Lock _sync = new();
    private readonly List<MediaPlayer> _mediaPlayers = [];
    private readonly LibVLC _libVlc;
    private bool _isDisposed;

    private LibVlcFactory(LibVLC libVlc) => _libVlc = libVlc;

    /// <summary>Number of native LibVLC instances created by this process; the contract is exactly one per option set.</summary>
    public static int NativeInstanceCount
    {
        get
        {
            lock (InstanceSync)
            {
                return SharedInstances.Count;
            }
        }
    }

    /// <summary>
    /// The options one native instance is built with, for the run that asked for them.
    /// </summary>
    /// <remarks>
    /// Exposed for the same reason as <see cref="NativeInstanceCount"/>: what is true of the native
    /// instance can only be asserted from outside if it can be read from outside. What matters here
    /// is what is <b>absent</b> — LibVLC takes its subtitle drawing from options given to the
    /// instance, and an instance is created once per option set and kept for the life of the
    /// process, so an option that is not in this list can never reach a drawing.
    /// </remarks>
    public static IReadOnlyList<string> InstanceOptions(bool headless) =>
        headless ? HeadlessOptions : BaseOptions;

    /// <summary>Media handed to <see cref="DeferRelease"/> and not yet disposed.</summary>
    public static int PendingDeferredReleaseCount
    {
        get
        {
            lock (ReleaseSync)
            {
                return DeferredReleases.Count;
            }
        }
    }

    /// <summary>Number of media players borrowed from this factory and not yet returned.</summary>
    public int LiveMediaPlayerCount
    {
        get
        {
            lock (_sync)
            {
                return _mediaPlayers.Count;
            }
        }
    }

    /// <summary>Creates a factory that decodes without a window or sound card, for automated runs.</summary>
    public static LibVlcFactory CreateHeadless() => Create(HeadlessOptions);

    /// <summary>Creates the factory used by the shell, rendering into a host-provided surface.</summary>
    public static LibVlcFactory CreateDefault() => Create(BaseOptions);

    public MediaPlayer CreateMediaPlayer()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        var mediaPlayer = new MediaPlayer(_libVlc);
        lock (_sync)
        {
            _mediaPlayers.Add(mediaPlayer);
        }

        return mediaPlayer;
    }

    public VlcMedia CreateMedia(string path)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new VlcMedia(_libVlc, path, FromType.FromPath);
    }

    /// <summary>
    /// Queues a media for disposal after the quiescence window, on a drain worker that survives a
    /// failing dispose — a dead worker would leak every native media from then on, silently.
    /// Callers release the media first and their player after it; the order is load-bearing.
    /// </summary>
    public void DeferRelease(VlcMedia media)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(media);
        lock (ReleaseSync)
        {
            DeferredReleases.Enqueue(new DeferredMedia(media, Stopwatch.GetTimestamp()));
            if (isDrainScheduled)
            {
                return;
            }

            isDrainScheduled = true;
        }

        _ = DrainDeferredReleasesAsync();
    }

    /// <summary>
    /// Waits for the deferred releases to finish, so a caller can let go of the player its media
    /// referenced. The wait has a ceiling and exhausting it returns <see langword="false"/> rather
    /// than throwing: a teardown that cannot complete is worse than a media that rests a moment
    /// longer, and the drain releases it either way. Waiting on another component's media is the
    /// price of one hardened queue, and it costs that media's quiescence window.
    /// </summary>
    public static async Task<bool> FlushDeferredReleasesAsync(TimeSpan ceiling)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ceiling, TimeSpan.Zero);
        var started = Stopwatch.GetTimestamp();
        while (PendingDeferredReleaseCount > 0)
        {
            if (Stopwatch.GetElapsedTime(started) >= ceiling)
            {
                return false;
            }

            await Task.Delay(FlushPollInterval).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Returns a borrowed media player. Callers must release every media first.</summary>
    public void ReleaseMediaPlayer(MediaPlayer mediaPlayer)
    {
        ArgumentNullException.ThrowIfNull(mediaPlayer);
        lock (_sync)
        {
            _ = _mediaPlayers.Remove(mediaPlayer);
        }

        mediaPlayer.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return ValueTask.CompletedTask;
        }

        _isDisposed = true;
        MediaPlayer[] outstanding;
        lock (_sync)
        {
            outstanding = [.. _mediaPlayers];
            _mediaPlayers.Clear();
        }

        foreach (var mediaPlayer in outstanding)
        {
            mediaPlayer.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private static async Task DrainDeferredReleasesAsync()
    {
        while (true)
        {
            DeferredMedia? due = null;
            TimeSpan remaining;
            lock (ReleaseSync)
            {
                if (!DeferredReleases.TryPeek(out var pending))
                {
                    isDrainScheduled = false;
                    return;
                }

                remaining = MediaQuiescence - Stopwatch.GetElapsedTime(pending.ReleasedTimestamp);
                if (remaining <= TimeSpan.Zero)
                {
                    due = DeferredReleases.Dequeue();
                }
            }

            if (due is not null)
            {
                try
                {
                    due.Media.Dispose();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // A native dispose that throws must not kill the drain: the next media still
                    // deserves its release, and a dead worker is a silent unbounded leak.
                }

                continue;
            }

            await Task.Delay(remaining).ConfigureAwait(false);
        }
    }

    private static LibVlcFactory Create(string[] options)
    {
        var key = string.Join(' ', options);
        lock (InstanceSync)
        {
            if (!SharedInstances.TryGetValue(key, out var instance))
            {
                Core.Initialize();
                instance = new LibVLC(options);
                SharedInstances[key] = instance;
            }

            return new LibVlcFactory(instance);
        }
    }

    private sealed record DeferredMedia(VlcMedia Media, long ReleasedTimestamp);
}
