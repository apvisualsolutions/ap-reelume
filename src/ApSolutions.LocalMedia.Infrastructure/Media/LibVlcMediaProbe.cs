// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace ApSolutions.LocalMedia.Infrastructure.Media;

/// <summary>
/// Reads technical metadata by parsing the file with LibVLC, on the one native instance this
/// process owns.
/// </summary>
/// <remarks>
/// The instance and the deferred release both belong to <see cref="LibVlcFactory"/> (BUG-010). This
/// class used to keep its own of each, which cost two things: the factory's "exactly one native
/// instance per option set" was false in any process that both probes and plays — and the count that
/// states it could not see the second one — and the private release worker had no guard around the
/// native dispose, so one throwing release would end the worker for good and leak every media probed
/// after it, silently. The factory's drain already survives that, and one hardened queue is worth
/// more than two of them.
/// <para>
/// Probes stay serialised: LibVLC is asked to parse one file at a time, which is how this repository
/// stopped crashing while cataloguing.
/// </para>
/// </remarks>
public sealed class LibVlcMediaProbe : IMediaProbe
{
    private static readonly SemaphoreSlim ProbeLock = new(1, 1);
    private readonly LibVlcFactory _factory;

    public LibVlcMediaProbe(LibVlcFactory factory) =>
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));

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
            var media = _factory.CreateMedia(path);
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
                // Releasing a media the instant its parse returns is the native failure mode behind
                // the whole deferral; the factory holds it for its quiescence window.
                _factory.DeferRelease(media);
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
}
