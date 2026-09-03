// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using LibVLCSharp.Shared;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace ApSolutions.LocalMedia.Infrastructure.Playback;

/// <summary>
/// Takes one frame out of a video with LibVLC and writes it as a PNG (CRS-006).
/// </summary>
/// <remarks>
/// <b>Everything this class decides lives one layer up.</b> Which lesson, which moment, whether the
/// picture is stale and how long to wait are <see cref="CourseThumbnailPolicy"/>'s, tested without a
/// decoder anywhere near them. What is left here is opening a file, seeking, asking for a snapshot
/// and waiting — and that is the shape the tenth rule asks for, so that only what genuinely needs a
/// machine is excluded from coverage.
/// <para>
/// <b>The route was measured before it was written</b> — «docs/evidence/stable/CRS-thumbnail-spike.md»,
/// 2026-09-03. The expectation going in was that this could not work at all: this application draws
/// video through LibVLC's callback path, and PLY-016 measured that VLC 3's filter chain never
/// processes a frame there. It does not carry over. A snapshot works with no video output attached,
/// which is what this uses, and the four decodable samples answered between 433 and 472 ms after the
/// seek.
/// </para>
/// <para>
/// <b>The extension is checked before anything is opened.</b> LibVLC decodes in-process in native
/// code and is this application's largest residual risk; handing it a path nobody filtered is the one
/// thing the trap notes forbid by name. This is called with paths out of the catalogue rather than
/// out of a dialog, which is exactly why the check is here and not left to the caller.
/// </para>
/// </remarks>
public sealed class LibVlcCourseFrameGrabber : ICourseFrameGrabber
{
    /// <summary>
    /// How wide a taken frame is written. The grid draws a 280 px card, so twice that covers a
    /// high-DPI screen without keeping a full frame of a 4K lesson on disk for every course.
    /// Zero height asks LibVLC to keep the source's own aspect ratio.
    /// </summary>
    private const uint ThumbnailWidth = 560;

    private readonly Func<string, IFrameCapture> _open;

    /// <summary>The adapter as the application uses it, over the one native instance there is.</summary>
    /// <remarks>
    /// <b>It borrows the factory rather than building an instance of its own</b>, which an earlier
    /// draft did and <c>NativeInstanceOwnershipTests</c> refused in the same minute. The rule is not
    /// stylistic: repeatedly building and tearing down LibVLC is the native failure mode this
    /// repository has already observed, and a thumbnail pass over a library is precisely the shape
    /// that would do it — one instance per course.
    /// </remarks>
    public LibVlcCourseFrameGrabber(LibVlcFactory factory)
        : this(path => new LibVlcCapture(factory, path))
    {
        ArgumentNullException.ThrowIfNull(factory);
    }

    /// <summary>
    /// The adapter over anything that can open and snapshot, which is what lets the waiting and the
    /// refusing be measured without a decoder.
    /// </summary>
    /// <remarks>
    /// Public, for the reason <c>IEndpointFormatStore</c> and <c>IEndpointFormatProbe</c> are: the
    /// seam between what decides and what talks to the machine is only worth anything if a test can
    /// stand on it, and the alternative is exempting the deadline from coverage because the only way
    /// to reach it is with a file no decoder understands.
    /// </remarks>
    public LibVlcCourseFrameGrabber(Func<string, IFrameCapture> open) =>
        _open = open ?? throw new ArgumentNullException(nameof(open));

    /// <summary>What this needs of a decoder, and nothing else.</summary>
    public interface IFrameCapture : IDisposable
    {
        /// <summary>Starts decoding and seeks to <paramref name="at"/>. False when it will not open.</summary>
        bool Start(TimeSpan at);

        /// <summary>Asks for a still to be written, and answers whether the ask was accepted.</summary>
        bool RequestSnapshot(string destinationPath, uint width);
    }

    /// <inheritdoc />
    public async Task<bool> TryCaptureAsync(
        string videoPath,
        TimeSpan at,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        // The approved containers, before a single native call. See the remarks above.
        if (!MediaFileExtensions.IsApproved(Path.GetExtension(videoPath)) || !File.Exists(videoPath))
        {
            return false;
        }

        using var capture = _open(videoPath);
        if (!capture.Start(at))
        {
            return false;
        }

        if (!capture.RequestSnapshot(destinationPath, ThumbnailWidth))
        {
            return false;
        }

        // The snapshot is written on LibVLC's own thread, so what says it happened is the file
        // appearing rather than the call returning. The deadline is the policy's, measured to sit
        // between the slowest success and the one file that never answers.
        var deadline = DateTimeOffset.UtcNow + CourseThumbnailPolicy.Deadline;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (File.Exists(destinationPath) && new FileInfo(destinationPath).Length > 0)
            {
                return true;
            }

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// The real decoder. Creating LibVLC objects and catching what only native code can raise.
    /// </summary>
    /// <remarks>
    /// <b>Excluded from coverage, and only this.</b> Every line here either constructs a native
    /// object or catches what a machine without a decoder throws; none of it decides anything. What
    /// decides — which frame, when to give up, whether to take it again — is
    /// <see cref="CourseThumbnailPolicy"/> and <see cref="GetCourseThumbnail"/>, both covered
    /// entirely without a video. That split is the tenth rule, and the reason this exclusion is
    /// narrow enough to be honest.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "Borrows LibVLC objects and catches native failures; every decision is in CourseThumbnailPolicy.")]
    private sealed class LibVlcCapture : IFrameCapture
    {
        private readonly LibVlcFactory _factory;
        private readonly VlcMedia _media;
        private readonly MediaPlayer _player;

        public LibVlcCapture(LibVlcFactory factory, string path)
        {
            _factory = factory;
            _media = factory.CreateMedia(path);
            _player = factory.CreateMediaPlayer();
        }

        public bool Start(TimeSpan at)
        {
            try
            {
                if (!_player.Play(_media))
                {
                    return false;
                }

                var waited = 0;
                while (_player.Length <= 0 && waited < 3000)
                {
                    Thread.Sleep(25);
                    waited += 25;
                }

                if (_player.Length <= 0)
                {
                    return false;
                }

                _player.Time = (long)at.TotalMilliseconds;
                Thread.Sleep(250);
                return true;
            }
            catch (VLCException)
            {
                return false;
            }
        }

        public bool RequestSnapshot(string destinationPath, uint width)
        {
            try
            {
                return _player.TakeSnapshot(0, destinationPath, width, 0);
            }
            catch (VLCException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                _player.Stop();
            }
            catch (VLCException)
            {
                // Stopping a player that already fell over is not something to report: both handles
                // are handed back either way.
            }

            _factory.ReleaseMediaPlayer(_player);

            // Released on the factory's own delay rather than here. Letting a media go the instant
            // its player does is the native failure mode this repository keeps relearning, and the
            // factory is where the waiting lives.
            _factory.DeferRelease(_media);
        }
    }
}
