// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Playback;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Draws the decoded picture as an ordinary Avalonia visual. Because the video is part of the
/// composition, accessible transport controls can be laid out above it.
/// </summary>
public sealed class VideoFrameView : Control, IDisposable
{
    public static readonly StyledProperty<IVideoFrameSource?> FrameSourceProperty =
        AvaloniaProperty.Register<VideoFrameView, IVideoFrameSource?>(nameof(FrameSource));

    /// <summary>
    /// Whether a picture smaller than the box it is drawn in is enhanced on its way there (PLY-016).
    /// </summary>
    /// <remarks>
    /// <b>It ships on</b>, which is what the owner asked for: the improvement reaches everybody
    /// without anybody finding a switch. Off is not a second code path — it lands on
    /// <see cref="UpscaleLink.CompositionBilinear"/>, which is the drawing this surface did before
    /// PLY-016 existed, so «off» and «today» are the same pixels rather than two things that have to
    /// be kept in step.
    /// </remarks>
    public static readonly StyledProperty<bool> IsUpscaleEnabledProperty =
        AvaloniaProperty.Register<VideoFrameView, bool>(nameof(IsUpscaleEnabled), defaultValue: true);

    private readonly Lock _sync = new();
    private WriteableBitmap? _bitmap;

    /// <summary>
    /// The frame the enhancement draws from, which is the array <see cref="OnFrameRendered"/> was
    /// already making and discarding. Keeping it adds no copying, and being a fresh array each frame
    /// is what makes it safe to read on the render thread while the next one is decoded.
    /// </summary>
    private byte[]? _lastFrame;
    private int _lastStride;

    /// <summary>
    /// The compiled shader, or null if it would not compile here. Compiled once for the life of the
    /// surface: compiling per frame would spend the budget the enhancement is measured against.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Deliberately not disposed.</b> A drawing operation that has already been handed to the
    /// scene graph runs on the render thread, and <see cref="Dispose"/> runs on the UI thread, so
    /// disposing this could free a native object while a frame in flight is still using it — a native
    /// crash, which is the worst failure class this application has. SkiaSharp's own finalizer
    /// releases it instead, and what is at stake is a few hundred bytes once per film.
    /// </para>
    /// <para>
    /// <b>That «once» went unguarded until ENG-017, and the gate auditor said so on 2026-09-13.</b>
    /// Deleting <c>_effectAsked</c> compiles the shader on every frame — about 173,000 of them in a
    /// two-hour film — and no test noticed, because nothing outside this class could count
    /// compilations. What it needed was a seam and not a louder comment, so
    /// <see cref="ShaderCompiler"/> is one: a test counts the calls and the count has to be one.
    /// </para>
    /// </remarks>
    private SKRuntimeEffect? _effect;
    private bool _effectAsked;

    public IVideoFrameSource? FrameSource
    {
        get => GetValue(FrameSourceProperty);
        set => SetValue(FrameSourceProperty, value);
    }

    public bool IsUpscaleEnabled
    {
        get => GetValue(IsUpscaleEnabledProperty);
        set => SetValue(IsUpscaleEnabledProperty, value);
    }

    /// <summary>
    /// Where the compiled shader comes from. A surface built without one asks
    /// <see cref="UpscaleShaderSource"/>, which is what the application does (ENG-017).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a seam and not a setting</b>, in the shape this folder already uses for them —
    /// <see cref="AudioOutputViewModel.SelectionHandler"/> and
    /// <see cref="PlayerViewModel.GestureHandler"/> — and it answers two questions no test could
    /// reach through this surface before.
    /// </para>
    /// <para>
    /// The first is how many times the shader is compiled, which is what ENG-017 was opened for. The
    /// second is what this surface draws when the shader does <b>not</b> compile: that arm belongs to
    /// a driver rejecting the SkSL, so on a machine where it compiles there is no way to enter it.
    /// It is the same argument, one storey up, that <see cref="UpscaleShaderSource.TryCompile"/>
    /// makes for taking its source as a parameter.
    /// </para>
    /// </remarks>
    public Func<SKRuntimeEffect?>? ShaderCompiler { get; set; }

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
            _lastFrame = null;
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

            // The picture keeps the shape it was decoded with: drawing into the whole bounds is
            // what stretched a 16:9 episode into whatever the window had been dragged to, here and
            // in the picture-in-picture. The bars are the surface showing through.
            var box = VideoFitPolicy.Fit(
                _bitmap.PixelSize.Width,
                _bitmap.PixelSize.Height,
                Bounds.Width,
                Bounds.Height);
            if (box.Width <= 0 || box.Height <= 0)
            {
                return;
            }

            var destination = new Rect(box.X, box.Y, box.Width, box.Height);
            var link = ChooseLink(_bitmap);
            if (link == UpscaleLink.CompositionBilinear || _lastFrame is null)
            {
                context.DrawImage(_bitmap, destination);
                return;
            }

            context.Custom(new SkiaUpscaleDrawOperation(
                _lastFrame,
                _bitmap.PixelSize.Width,
                _bitmap.PixelSize.Height,
                _lastStride,
                destination,
                link,
                _effect,
                _bitmap));
        }
    }

    /// <summary>
    /// Which link of PLY-016's chain draws this frame. Held under <c>_sync</c> by its only caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both of the rules that switch the chain off live in the policy and not here.</b> An earlier
    /// draft repeated them — an early return for the switch and another for a picture that is not
    /// being enlarged — and a mutation showed why that is wrong: inverting the local guard changed
    /// nothing, because <see cref="UpscaleChainPolicy.Choose"/> already answered the same question.
    /// A guard no test can tell apart from its own absence is not a guard.
    /// </para>
    /// <para>
    /// Two of the four capabilities are reported absent, and the reason is measured rather than
    /// unfinished: the card makers' own super resolution and the driver's edge enhancement both live
    /// behind the Direct3D video processor, and <b>there is no way to hand its result to the
    /// composition</b> — the compositor answers that it has no GPU interop. The remaining route is to
    /// read the texture back into system memory, which is 33 MB a frame at 4K; <i>that</i> figure is
    /// the size and not a timing — the probe's per-frame costs subtract the read-back on purpose, so
    /// nobody has clocked it. They stay in the chain because the chain is the chain; what unblocks
    /// them is a decoder that is not copyleft and then the RTX video kit, which runs inside the
    /// process and needs neither a shared texture nor a read-back.
    /// </para>
    /// <para>
    /// Cubic resampling is reported present without asking, because whether this renderer is Skia is
    /// only knowable on the render thread. The operation degrades to exactly today's drawing when it
    /// is not, so claiming it here costs a frame nothing and saves asking a question on the wrong
    /// thread.
    /// </para>
    /// </remarks>
    private UpscaleLink ChooseLink(WriteableBitmap bitmap)
    {
        if (!_effectAsked)
        {
            _effectAsked = true;
            _effect = (ShaderCompiler ?? CompileShader)();
        }

        return UpscaleChainPolicy.Choose(
            IsUpscaleEnabled,
            UpscalePolicy.Decide(
                bitmap.PixelSize.Width,
                bitmap.PixelSize.Height,
                (int)Bounds.Width,
                (int)Bounds.Height),
            new UpscaleCapabilities(
                VendorSuperResolution: false,
                VideoProcessorEnhancement: false,
                RuntimeShader: _effect is not null,
                CubicResampler: true));
    }

    /// <summary>
    /// What <see cref="ShaderCompiler"/> falls back to, which is the application's only compiler.
    /// The error text is dropped because a shader that will not compile here is not a fault to
    /// report: the chain answers it by handing the frame to the link below.
    /// </summary>
    private static SKRuntimeEffect? CompileShader() => UpscaleShaderSource.TryCompile(out _);

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

            // Kept rather than discarded, which is what makes the enhancement free of extra copying.
            _lastFrame = pixels;
            _lastStride = frame.Stride;
        }

        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }
}
