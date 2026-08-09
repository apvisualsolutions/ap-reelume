using System.Diagnostics;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace ApSolutions.LocalMedia.Infrastructure.Media;

public sealed class LibVlcMediaProbe : IMediaProbe
{
    private static readonly TimeSpan NativeMediaQuiescence = TimeSpan.FromSeconds(1);
    private static readonly SemaphoreSlim ProbeLock = new(1, 1);
    private static readonly Queue<DeferredMedia> DeferredMediaReleases = new();
    private static readonly Lazy<LibVLC> SharedLibVlc = new(CreateLibVlc);
    private static bool releaseWorkerScheduled;

    public async Task<TechnicalMetadata> ProbeAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The media file could not be found.", path);
        }

        await ProbeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var media = new VlcMedia(SharedLibVlc.Value, path, FromType.FromPath);
            try
            {
                var status = await media
                    .Parse(MediaParseOptions.ParseLocal, timeout: 10_000, cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var tracks = media.Tracks;
                if (status != MediaParsedStatus.Done || tracks.Length == 0)
                {
                    throw new InvalidDataException($"LibVLC could not read technical metadata from '{path}'.");
                }

                var videoTracks = tracks.Where(track => track.TrackType == TrackType.Video).ToArray();
                var audioTracks = tracks.Where(track => track.TrackType == TrackType.Audio).ToArray();
                var firstVideo = videoTracks.FirstOrDefault();
                return new TechnicalMetadata(
                    media.Duration > 0 ? TimeSpan.FromMilliseconds(media.Duration) : null,
                    Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),
                    ReadCodecs(media, videoTracks),
                    ReadCodecs(media, audioTracks),
                    firstVideo.TrackType == TrackType.Video ? checked((int)firstVideo.Data.Video.Width) : null,
                    firstVideo.TrackType == TrackType.Video ? checked((int)firstVideo.Data.Video.Height) : null);
            }
            finally
            {
                DeferredMediaReleases.Enqueue(new DeferredMedia(media, Stopwatch.GetTimestamp()));
                ScheduleDeferredRelease();
            }
        }
        finally
        {
            ProbeLock.Release();
        }
    }

    private static string[] ReadCodecs(VlcMedia media, IEnumerable<MediaTrack> tracks) => tracks
        .Select(track => media.CodecDescription(track.TrackType, track.Codec))
        .Where(description => !string.IsNullOrWhiteSpace(description))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static LibVLC CreateLibVlc()
    {
        Core.Initialize();
        return new LibVLC(
            "--no-metadata-network-access",
            "--no-sub-autodetect-file",
            "--no-video-title-show");
    }

    private static void ScheduleDeferredRelease()
    {
        if (releaseWorkerScheduled)
        {
            return;
        }

        releaseWorkerScheduled = true;
        _ = ReleaseWhenQuiescedAsync();
    }

    private static async Task ReleaseWhenQuiescedAsync()
    {
        while (true)
        {
            TimeSpan remaining;
            await ProbeLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!DeferredMediaReleases.TryPeek(out var pending))
                {
                    releaseWorkerScheduled = false;
                    return;
                }

                remaining = NativeMediaQuiescence
                    - Stopwatch.GetElapsedTime(pending.CompletedTimestamp);
                if (remaining <= TimeSpan.Zero)
                {
                    _ = DeferredMediaReleases.Dequeue();
                    pending.Media.Dispose();
                    continue;
                }
            }
            finally
            {
                ProbeLock.Release();
            }

            await Task.Delay(remaining).ConfigureAwait(false);
        }
    }

    private sealed record DeferredMedia(VlcMedia Media, long CompletedTimestamp);
}
