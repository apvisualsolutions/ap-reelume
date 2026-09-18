// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Courses;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// Takes a frame of its own video for every title with no picked cover and no provider poster, and
/// keeps it where <see cref="ResolveTitlePoster"/> looks (LIB-021, ADR-0009).
/// </summary>
/// <remarks>
/// <para>
/// <b>A pass in the background, not a capture while a card is painted.</b> The grid builds its cards
/// synchronously and a decode takes up to <see cref="CourseThumbnailPolicy.Deadline"/>; asking for
/// frames there would either stall the grid or leave it full of loose tasks.
/// </para>
/// <para>
/// <b>It opens videos on its own</b>, which is the widening of LibVLC's use the ADR accepted with its
/// limit: only files the catalogue already has on a disk that is here, once per title — an unchanged
/// file is never decoded twice, by the same stamp course thumbnails use — and the result kept.
/// </para>
/// <para>
/// It yields to somebody watching and to a scan, checked before every title rather than once, the
/// way <c>RefreshStaleMetadata</c> does: a pass outlives the moment it started in.
/// </para>
/// </remarks>
public sealed class CaptureTitleFrames
{
    /// <summary>
    /// How many titles are walked between two refreshes of the grid. Telling it after every frame
    /// would make a large library flicker; only at the end would leave a new one bare for minutes.
    /// </summary>
    public const int BatchSize = 25;

    private readonly ITitleFrameSources _sources;
    private readonly IVideoFrameGrabber _grabber;
    private readonly IAppDataPaths _paths;
    private readonly IPlaybackActivity _playback;
    private readonly IScanActivity _scans;

    public CaptureTitleFrames(
        ITitleFrameSources sources,
        IVideoFrameGrabber grabber,
        IAppDataPaths paths,
        IPlaybackActivity playback,
        IScanActivity scans)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _grabber = grabber ?? throw new ArgumentNullException(nameof(grabber));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _scans = scans ?? throw new ArgumentNullException(nameof(scans));
    }

    private bool IsBusy => _playback.IsPlaybackActive || _scans.IsScanActive;

    /// <summary>Walks the titles that need a frame and answers how many it took.</summary>
    /// <param name="afterBatch">
    /// Called after every <see cref="BatchSize"/> titles that took at least one frame, and once more
    /// for the remainder, so whoever draws the grid can redraw it.
    /// </param>
    public async Task<int> ExecuteAsync(Func<Task>? afterBatch = null, CancellationToken cancellationToken = default)
    {
        var sources = await _sources.ListWithoutCoverAsync(cancellationToken).ConfigureAwait(false);
        var taken = 0;
        var takenInBatch = 0;
        var walked = 0;
        foreach (var source in sources)
        {
            if (IsBusy)
            {
                break;
            }

            if (await TryTakeAsync(source, cancellationToken).ConfigureAwait(false))
            {
                taken++;
                takenInBatch++;
            }

            walked++;
            if (walked % BatchSize == 0)
            {
                takenInBatch = await TellAsync(afterBatch, takenInBatch).ConfigureAwait(false);
            }
        }

        _ = await TellAsync(afterBatch, takenInBatch).ConfigureAwait(false);
        return taken;
    }

    private static async Task<int> TellAsync(Func<Task>? afterBatch, int takenInBatch)
    {
        if (takenInBatch > 0 && afterBatch is not null)
        {
            await afterBatch().ConfigureAwait(false);
        }

        return 0;
    }

    private async Task<bool> TryTakeAsync(TitleFrameSource source, CancellationToken cancellationToken)
    {
        var destination = ResolveTitlePoster.FrameFileFor(_paths, source.TitleId);
        var action = CourseThumbnailPolicy.Decide(hasSource: true, FrameStampFile.Read(destination), source.Stamp);
        if (action != CourseThumbnailAction.Capture)
        {
            return false;
        }

        Directory.CreateDirectory(_paths.TitleFrameDirectory);
        var at = CourseThumbnailPolicy.SeekPosition(source.Duration ?? TimeSpan.Zero);
        if (!await _grabber.TryCaptureAsync(source.VideoPath, at, destination, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        FrameStampFile.Write(destination, source.Stamp);
        return true;
    }
}
