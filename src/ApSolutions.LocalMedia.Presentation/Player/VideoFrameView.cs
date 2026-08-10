// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Playback;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Draws the decoded picture as an ordinary Avalonia visual. Because the video is part of the
/// composition, accessible transport controls can be laid out above it.
/// </summary>
public sealed class VideoFrameView : Control, IDisposable
{
    public static readonly StyledProperty<IVideoFrameSource?> FrameSourceProperty =
        AvaloniaProperty.Register<VideoFrameView, IVideoFrameSource?>(nameof(FrameSource));

    private readonly Lock _sync = new();
    private WriteableBitmap? _bitmap;

    public IVideoFrameSource? FrameSource
    {
        get => GetValue(FrameSourceProperty);
        set => SetValue(FrameSourceProperty, value);
    }

    public void Dispose()
    {
        if (FrameSource is { } source)
        {
            source.FrameRendered -= OnFrameRendered;
        }

        lock (_sync)
        {
            _bitmap?.Dispose();
            _bitmap = null;
        }
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        base.Render(context);
        lock (_sync)
        {
            if (_bitmap is null)
            {
                return;
            }

            context.DrawImage(_bitmap, new Rect(Bounds.Size));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property != FrameSourceProperty)
        {
            return;
        }

        if (change.GetOldValue<IVideoFrameSource?>() is { } previous)
        {
            previous.FrameRendered -= OnFrameRendered;
        }

        if (change.GetNewValue<IVideoFrameSource?>() is { } current)
        {
            current.FrameRendered += OnFrameRendered;
        }
    }

    private void OnFrameRendered(object? sender, VideoFrameEventArgs frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_bitmap is null
                || _bitmap.PixelSize.Width != frame.Width
                || _bitmap.PixelSize.Height != frame.Height)
            {
                _bitmap?.Dispose();
                _bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Opaque);
            }

            using var buffer = _bitmap.Lock();
            var pixels = frame.Pixels.ToArray();
            Marshal.Copy(pixels, 0, buffer.Address, Math.Min(pixels.Length, buffer.RowBytes * frame.Height));
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }
}
