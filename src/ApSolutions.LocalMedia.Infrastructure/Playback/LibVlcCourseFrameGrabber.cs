// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
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
/// decoder anywhere near them. What is left here is opening a file, seeking, keeping a frame and
/// encoding it.
/// <para>
/// <b>IT ASKED LIBVLC FOR THE FILE UNTIL 2026-09-03, AND THAT DOES NOT SURVIVE A MACHINE WITH NO
/// SCREEN.</b> <c>TakeSnapshot</c> works on a developer's desktop and produced <b>no frame at all</b>
/// on a hosted runner — measured, one red build. A snapshot is written by the video output and a
/// runner has none. The frames the callback path hands over need no output of any kind: they are the
/// same frames this application already paints with, and the spike had measured them arriving in
/// 137 ms while the snapshot route was never measured anywhere but here. <b>Choosing the route that
/// only works where it was tried</b> is the mistake this paragraph exists to stop repeating.
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
    /// The adapter over anything that can open a file and hand back a frame, which is what lets the
    /// waiting and the refusing be measured without a decoder.
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

        /// <summary>
        /// The first frame decoded after the seek, or nothing if none arrived within
        /// <paramref name="deadline"/>.
        /// </summary>
        CapturedFrame? WaitForFrame(TimeSpan deadline);
    }

    /// <summary>One decoded frame, as it comes off the decoder.</summary>
    /// <param name="Pixels">Blue-first, four bytes a pixel, which is LibVLC's RV32.</param>
    /// <param name="Width">Pixels across.</param>
    /// <param name="Height">Rows.</param>
    /// <param name="Stride">
    /// Bytes per row, which a decoder aligns and is not always four times the width.
    /// </param>
    public readonly record struct CapturedFrame(byte[] Pixels, int Width, int Height, int Stride);

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

        // On a pool thread because the wait below is a native event rather than a task: blocking the
        // caller's thread here would stall whatever asked for the picture, and a grid of courses asks
        // for many.
        return await Task.Run(
            () =>
            {
                using var capture = _open(videoPath);
                if (!capture.Start(at))
                {
                    return false;
                }

                if (capture.WaitForFrame(CourseThumbnailPolicy.Deadline) is not { } frame)
                {
                    return false;
                }

                PngWriter.WriteBgra(frame.Pixels, frame.Width, frame.Height, frame.Stride, destinationPath);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The real decoder: LibVLC's callback path, which is how this application already receives every
    /// frame it paints.
    /// </summary>
    /// <remarks>
    /// <b>Excluded from coverage, and only this.</b> Every line here either borrows a native object,
    /// marshals a buffer or catches what a machine without a decoder throws; none of it decides
    /// anything. What decides is <see cref="CourseThumbnailPolicy"/> and
    /// <see cref="GetCourseThumbnail"/>, and every byte written is <see cref="PngWriter"/>'s — all
    /// three covered without a video anywhere near them. That split is the tenth rule, and the reason
    /// this exclusion is narrow enough to be honest.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "Borrows LibVLC objects and marshals its buffers; every decision is in CourseThumbnailPolicy and every byte written is PngWriter's.")]
    private sealed class LibVlcCapture : IFrameCapture
    {
        private readonly LibVlcFactory _factory;
        private readonly VlcMedia _media;
        private readonly MediaPlayer _player;
        private readonly ManualResetEventSlim _arrived = new(false);
        private readonly Lock _sync = new();

        // Held for the life of the capture because LibVLC keeps the pointers: a delegate collected
        // while the decoder still holds it is a native crash rather than an exception.
        private readonly MediaPlayer.LibVLCVideoFormatCb _format;
        private readonly MediaPlayer.LibVLCVideoCleanupCb _cleanup;
        private readonly MediaPlayer.LibVLCVideoLockCb _lock;
        private readonly MediaPlayer.LibVLCVideoDisplayCb _display;

        private nint _buffer;
        private uint _width;
        private uint _height;
        private uint _stride;
        private uint _lines;
        private bool _seeked;
        private CapturedFrame? _kept;

        public LibVlcCapture(LibVlcFactory factory, string path)
        {
            _factory = factory;
            _media = factory.CreateMedia(path);
            _player = factory.CreateMediaPlayer();

            _format = (ref nint opaque, nint chroma, ref uint width, ref uint height, ref uint pitch, ref uint lines) =>
            {
                WriteFourCc(chroma, "RV32");
                pitch = width * 4;
                lines = height;
                _width = width;
                _height = height;
                _stride = pitch;
                _lines = lines;
                _buffer = Marshal.AllocHGlobal((int)(pitch * lines));
                return 1;
            };

            _cleanup = (ref nint opaque) =>
            {
                if (_buffer != 0)
                {
                    Marshal.FreeHGlobal(_buffer);
                    _buffer = 0;
                }
            };

            _lock = (nint opaque, nint planes) =>
            {
                Marshal.WriteIntPtr(planes, _buffer);
                return 0;
            };

            _display = (nint opaque, nint picture) =>
            {
                lock (_sync)
                {
                    // Only after the seek has landed: the frames before it are the ones at zero,
                    // which is a black frame or a title card in almost every video anybody records.
                    if (!_seeked || _kept is not null || _buffer == 0)
                    {
                        return;
                    }

                    var bytes = new byte[_stride * _lines];
                    Marshal.Copy(_buffer, bytes, 0, bytes.Length);
                    _kept = new CapturedFrame(bytes, (int)_width, (int)_height, (int)_stride);
                }

                _arrived.Set();
            };
        }

        public bool Start(TimeSpan at)
        {
            try
            {
                _player.SetVideoFormatCallbacks(_format, _cleanup);
                _player.SetVideoCallbacks(_lock, null, _display);

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
                lock (_sync)
                {
                    _seeked = true;
                }

                return true;
            }
            catch (VLCException)
            {
                return false;
            }
        }

        public CapturedFrame? WaitForFrame(TimeSpan deadline)
        {
            _ = _arrived.Wait(deadline);
            lock (_sync)
            {
                return _kept;
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
            _arrived.Dispose();
        }

        private static void WriteFourCc(nint chroma, string code)
        {
            for (var i = 0; i < 4; i++)
            {
                Marshal.WriteByte(chroma, i, (byte)code[i]);
            }
        }
    }
}
