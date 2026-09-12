// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// PLY-016's measurement: asks every video adapter in this machine which picture formats its video
/// processor takes, whether it accepts the vendor's super-resolution extension, and — the only part
/// that proves anything — whether the picture that comes out differs with the extension on and off.
/// </summary>
/// <remarks>
/// <para>
/// A returned <c>S_OK</c> proves nothing here. <c>CheckVideoProcessorFormat</c> succeeds for formats
/// it refuses, and <c>VideoProcessorSetStreamExtension</c> succeeds on a driver that then ignores the
/// request — NVIDIA's does exactly that until somebody switches RTX Video Super Resolution on in the
/// NVIDIA app. Only compared pixels separate "it ran" from "it was accepted".
/// </para>
/// <para>
/// The extension GUIDs, their payloads and the call sequence follow VLC's
/// <c>modules/video_output/win32/d3d11_scaler.cpp</c> (LGPL-2.1-or-later). Every vtable slot number
/// below was read on 2026-09-12 from the 10.0.26100.0 Windows SDK headers
/// (<c>um\d3d11.h</c>, <c>shared\dxgi.h</c>) and not from documentation: the published method order
/// differs from the header's, and <c>CreateVideoProcessor</c> sits at slot 4 while
/// <c>CreateVideoProcessorEnumerator</c> — which is called first — sits at 10.
/// </para>
/// <para>
/// Excluded from coverage as a whole, which is rule 10's seam and not an exception to it: everything
/// that decides anything lives in <see cref="D3d11UpscaleFormats"/> and is measured there on any
/// machine. What is left can only fail if Windows or a display driver fails.
/// </para>
/// <para>
/// <b>And the exclusion is measured, not assumed — twice, in both directions.</b> CI first rejected
/// this file at 45/100 because the data records then living beside it were <i>not</i> excluded and
/// the gate measures per file; moving them out turned the run green. Then the exclusion was taken off
/// anyway, on the reasoning that no other file in the tree is excluded whole and that the coverage
/// preview reports this one as «not measured» — and the green run had already proved the gate
/// accepts it. The preview's complaint is a limit of the preview, which cannot judge a file with no
/// measurable lines; it is not the gate refusing. Taking it off turned the next run red and put a
/// number on how hardware-bound this file is: <b>15/10 on a hosted runner against 92/63 on a machine
/// with two graphics cards</b>. It would have become an eighth entry in <c>eng/coverage-debt.txt</c>
/// and a permanently higher ratchet, bought with a deduction against a measurement.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public static class WindowsVideoUpscaleProbe
{
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const uint SdkVersion = 7;
    private const int DriverTypeUnknown = 0;
    private const uint UsageDefault = 0;
    private const uint UsageStaging = 3;
    private const uint BindRenderTarget = 0x20;
    private const uint CpuAccessRead = 0x20000;
    private const uint MapRead = 1;
    private const uint FrameFormatProgressive = 0;
    private const uint VideoUsagePlaybackNormal = 0;
    private const uint ViewDimensionTexture2D = 1;

    /// <summary>
    /// The packed bitfield <c>D3D11_VIDEO_PROCESSOR_COLOR_SPACE</c>: bit 0 usage, bit 1 RGB range,
    /// bit 2 YCbCr matrix, bit 3 xvYCC, bits 4-5 nominal range. Playback, BT.709 (bit 2), studio
    /// range (bits 4-5 = 1) gives 0b010100.
    /// </summary>
    private static uint StreamColorSpace = 0b01_0100u;

    /// <summary>Playback, full-range RGB: every field zero.</summary>
    private static uint OutputColorSpace;

    private static readonly Guid IidDxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
    private static readonly Guid IidVideoDevice = new("10EC4D5B-975A-4689-B9E4-D0AAC30FE333");
    private static readonly Guid IidVideoContext = new("61F21C45-3C0E-4a74-9CEA-67100D9AD5E4");

    /// <summary>
    /// Asks every adapter the machine reports. An adapter that cannot be opened, or whose driver has
    /// no video processor, is still returned — with its note — because an adapter missing from the
    /// report reads as an adapter that was never asked.
    /// </summary>
    public static IReadOnlyList<AdapterUpscaleProbe> ProbeAll(
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        UpscaleProbeContent content = UpscaleProbeContent.Bands,
        string? forcedInput = null)
    {
        var results = new List<AdapterUpscaleProbe>();
        var factoryId = IidDxgiFactory1;
        if (CreateDXGIFactory1(ref factoryId, out var factory) < 0)
        {
            return results;
        }

        try
        {
            var enumAdapters = Com.Method<EnumAdapters1Fn>(factory, 12);
            for (var index = 0u; ; index++)
            {
                if (enumAdapters(factory, index, out var adapter) == DxgiErrorNotFound)
                {
                    break;
                }

                try
                {
                    results.Add(ProbeAdapter(
                        adapter, sourceWidth, sourceHeight, targetWidth, targetHeight, content, forcedInput));
                }
                finally
                {
                    Com.Release(adapter);
                }
            }
        }
        finally
        {
            Com.Release(factory);
        }

        return results;
    }

    private static AdapterUpscaleProbe ProbeAdapter(
        nint adapter,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        UpscaleProbeContent content,
        string? forcedInput)
    {
        var desc = new AdapterDesc1 { Description = string.Empty };
        _ = Com.Method<GetDesc1Fn>(adapter, 10)(adapter, ref desc);
        var description = desc.Description;
        var vendor = D3d11UpscaleFormats.VendorOf(desc.VendorId);
        var support = new Dictionary<string, uint>(StringComparer.Ordinal);

        var created = D3D11CreateDevice(
            adapter, DriverTypeUnknown, nint.Zero, 0, nint.Zero, 0, SdkVersion,
            out var device, out _, out var context);
        if (created < 0)
        {
            return Blocked(description, desc.VendorId, vendor, support, $"D3D11CreateDevice: 0x{created:X8}");
        }

        try
        {
            if (Com.QueryInterface(device, IidVideoDevice, out var videoDevice) < 0)
            {
                return Blocked(description, desc.VendorId, vendor, support, "this driver exposes no ID3D11VideoDevice.");
            }

            if (Com.QueryInterface(context, IidVideoContext, out var videoContext) < 0)
            {
                Com.Release(videoDevice);
                return Blocked(description, desc.VendorId, vendor, support, "this driver exposes no ID3D11VideoContext.");
            }

            try
            {
                return ProbeVideoDevice(
                    new Devices(device, context, videoDevice, videoContext),
                    description,
                    desc.VendorId,
                    vendor,
                    support,
                    sourceWidth,
                    sourceHeight,
                    targetWidth,
                    targetHeight,
                    content,
                    forcedInput);
            }
            finally
            {
                Com.Release(videoContext);
                Com.Release(videoDevice);
            }
        }
        finally
        {
            Com.Release(context);
            Com.Release(device);
        }
    }

    private static AdapterUpscaleProbe ProbeVideoDevice(
        Devices devices,
        string description,
        uint vendorId,
        GpuVendor vendor,
        Dictionary<string, uint> support,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        UpscaleProbeContent content,
        string? forcedInput)
    {
        var contentDesc = new VideoProcessorContentDesc
        {
            InputFrameFormat = FrameFormatProgressive,
            InputFrameRateNumerator = 25,
            InputFrameRateDenominator = 1,
            InputWidth = (uint)sourceWidth,
            InputHeight = (uint)sourceHeight,
            OutputFrameRateNumerator = 25,
            OutputFrameRateDenominator = 1,
            OutputWidth = (uint)targetWidth,
            OutputHeight = (uint)targetHeight,
            Usage = VideoUsagePlaybackNormal,
        };

        var createEnumerator = Com.Method<CreateVideoProcessorEnumeratorFn>(devices.VideoDevice, 10);
        var result = createEnumerator(devices.VideoDevice, ref contentDesc, out var enumerator);
        if (result < 0)
        {
            return Blocked(description, vendorId, vendor, support, $"CreateVideoProcessorEnumerator: 0x{result:X8}");
        }

        try
        {
            var check = Com.Method<CheckVideoProcessorFormatFn>(enumerator, 8);
            foreach (var format in D3d11UpscaleFormats.Battery)
            {
                // The flags are the answer; the result code is not. A format the processor refuses
                // still returns S_OK and writes zero flags.
                support[format.Name] = check(enumerator, format.DxgiFormat, out var flags) < 0 ? 0u : flags;
            }

            var preferred = forcedInput ?? D3d11UpscaleFormats.PreferredInput(support);
            if (preferred is null)
            {
                return new AdapterUpscaleProbe(
                    description, vendorId, vendor, support, null, null, null, null,
                    "the processor takes no format this pipeline can produce, so no picture was sent.");
            }

            return Blt(
                devices, enumerator, description, vendorId, vendor, support, preferred,
                sourceWidth, sourceHeight, targetWidth, targetHeight, content);
        }
        finally
        {
            Com.Release(enumerator);
        }
    }

    private static AdapterUpscaleProbe Blt(
        Devices devices,
        nint enumerator,
        string description,
        uint vendorId,
        GpuVendor vendor,
        Dictionary<string, uint> support,
        string preferred,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int targetHeight,
        UpscaleProbeContent content)
    {
        var inputFormat = D3d11UpscaleFormats.Battery.First(format => format.Name == preferred);
        var pattern = D3d11UpscaleFormats.TestPattern(
            preferred, sourceWidth, sourceHeight, content, out var pitch);
        var handle = GCHandle.Alloc(pattern, GCHandleType.Pinned);
        var processor = nint.Zero;
        var input = nint.Zero;
        var inputView = nint.Zero;
        var output = nint.Zero;
        var outputView = nint.Zero;
        var staging = nint.Zero;

        try
        {
            var created = Com.Method<CreateVideoProcessorFn>(devices.VideoDevice, 4)(
                devices.VideoDevice, enumerator, 0, out processor);
            if (created < 0)
            {
                return Blocked(description, vendorId, vendor, support, $"CreateVideoProcessor: 0x{created:X8}", preferred);
            }

            var initial = new SubresourceData
            {
                SysMem = handle.AddrOfPinnedObject(),
                SysMemPitch = (uint)pitch,
            };
            var createTexture = Com.Method<CreateTexture2DFn>(devices.Device, 5);
            var createTextureWithData = Com.Method<CreateTexture2DWithDataFn>(devices.Device, 5);
            var inputDesc = Texture(sourceWidth, sourceHeight, inputFormat.DxgiFormat, UsageDefault, 0, 0);
            var uploaded = createTextureWithData(devices.Device, ref inputDesc, ref initial, out input);
            if (uploaded < 0)
            {
                return Blocked(
                    description, vendorId, vendor, support,
                    $"CreateTexture2D refused a {preferred} input: 0x{uploaded:X8}", preferred);
            }

            if (CreateOutputs(devices, createTexture, targetWidth, targetHeight, out output, out staging) is { } failure)
            {
                return Blocked(description, vendorId, vendor, support, failure, preferred);
            }

            var inputViewDesc = new VideoProcessorInputViewDesc { ViewDimension = ViewDimensionTexture2D };
            var outputViewDesc = new VideoProcessorOutputViewDesc { ViewDimension = ViewDimensionTexture2D };
            if (Com.Method<CreateVideoProcessorInputViewFn>(devices.VideoDevice, 8)(
                    devices.VideoDevice, input, enumerator, ref inputViewDesc, out inputView) < 0
                || Com.Method<CreateVideoProcessorOutputViewFn>(devices.VideoDevice, 9)(
                    devices.VideoDevice, output, enumerator, ref outputViewDesc, out outputView) < 0)
            {
                return Blocked(description, vendorId, vendor, support, "the processor refused a view over the textures.", preferred);
            }

            // VLC's scaler declares both colour spaces before it draws, and so does this: leaving
            // them at their defaults is one more reason a vendor's super resolution could decline
            // that has nothing to do with whether the machine supports it.
            //
            // Stream: broadcast-range YCbCr on the BT.709 matrix, which is what YUY2 from a modern
            // decoder carries. Output: full-range RGB. Both are the packed bitfield d3d11.h defines.
            Com.Method<SetStreamColorSpaceFn>(devices.VideoContext, 28)(
                devices.VideoContext, processor, 0, ref StreamColorSpace);
            Com.Method<SetOutputColorSpaceFn>(devices.VideoContext, 15)(
                devices.VideoContext, processor, ref OutputColorSpace);

            var onResult = SetExtension(
                devices.VideoContext, processor, vendor, enable: true, out var refusedFunction, out var onRequested);
            var withExtension = Draw(devices, processor, inputView, outputView, output, staging, targetWidth, targetHeight);

            var offResult = SetExtension(
                devices.VideoContext, processor, vendor, enable: false, out _, out var offRequested);
            var without = Draw(devices, processor, inputView, outputView, output, staging, targetWidth, targetHeight);
            var controlRun = Draw(devices, processor, inputView, outputView, output, staging, targetWidth, targetHeight);

            var pixels = new UpscalePixelComparison(
                D3d11UpscaleFormats.CountDifferences(withExtension.Pixels, without.Pixels),
                D3d11UpscaleFormats.CountDifferences(without.Pixels, controlRun.Pixels),
                withExtension.Pixels.Length,
                withExtension.Pixels.Distinct().Count(),
                Math.Min(Math.Min(withExtension.Result, without.Result), controlRun.Result),
                withExtension.Pixels.Length == without.Pixels.Length
                    && without.Pixels.Length == controlRun.Pixels.Length);

            var filters = MeasureStandardFilters(
                devices, enumerator, processor, inputView, outputView, output, staging,
                without.Pixels, targetWidth, targetHeight);

            var note = onResult >= 0
                ? null
                : FormattableString.Invariant(
                    $"the extension was refused on function 0x{refusedFunction:X2} with 0x{onResult:X8}.");
            return new AdapterUpscaleProbe(
                description,
                vendorId,
                vendor,
                support,
                preferred,
                vendor is GpuVendor.Nvidia or GpuVendor.Intel
                    ? new VendorExtensionOutcome(onResult, offResult, refusedFunction, onRequested, offRequested)
                    : null,
                filters,
                pixels,
                note);
        }
        finally
        {
            Com.Release(staging);
            Com.Release(outputView);
            Com.Release(output);
            Com.Release(inputView);
            Com.Release(input);
            Com.Release(processor);
            handle.Free();
        }
    }

    /// <summary>
    /// What the processor offers with nobody switching anything on. These filters belong to
    /// Direct3D rather than to a graphics card company, so they need no vendor application, no user
    /// setting and no licence — which makes them the floor under this feature rather than a bonus.
    /// </summary>
    private static StandardFilterOutcome MeasureStandardFilters(
        Devices devices,
        nint enumerator,
        nint processor,
        nint inputView,
        nint outputView,
        nint output,
        nint staging,
        byte[] plain,
        int targetWidth,
        int targetHeight)
    {
        var caps = new VideoProcessorCaps();
        _ = Com.Method<GetVideoProcessorCapsFn>(enumerator, 9)(enumerator, ref caps);
        var offered = D3d11UpscaleFormats.OfferedFilters(caps.FilterCaps);
        var edge = D3d11UpscaleFormats.StandardFilters.First(filter => filter.Name == "EDGE_ENHANCEMENT");
        if (!offered.Contains("EDGE_ENHANCEMENT"))
        {
            return new StandardFilterOutcome(caps.FilterCaps, offered, -1);
        }

        var range = new VideoProcessorFilterRange();
        _ = Com.Method<GetFilterRangeFn>(enumerator, 12)(enumerator, edge.Index, ref range);
        var setFilter = Com.Method<SetStreamFilterFn>(devices.VideoContext, 38);

        setFilter(devices.VideoContext, processor, 0, edge.Index, enabled: 1, range.Maximum);
        var enhanced = Draw(devices, processor, inputView, outputView, output, staging, targetWidth, targetHeight);
        setFilter(devices.VideoContext, processor, 0, edge.Index, enabled: 0, range.Default);

        return new StandardFilterOutcome(
            caps.FilterCaps, offered, D3d11UpscaleFormats.CountDifferences(enhanced.Pixels, plain));
    }

    private static string? CreateOutputs(
        Devices devices,
        CreateTexture2DFn createTexture,
        int targetWidth,
        int targetHeight,
        out nint output,
        out nint staging)
    {
        staging = nint.Zero;

        // The picture comes back as BGRA whatever went in: it is what Avalonia's composition imports
        // and what the readback below counts, so the measurement never depends on the input format.
        var outputDesc = Texture(targetWidth, targetHeight, 87u, UsageDefault, BindRenderTarget, 0);
        var created = createTexture(devices.Device, ref outputDesc, nint.Zero, out output);
        if (created < 0)
        {
            return $"CreateTexture2D refused the output: 0x{created:X8}";
        }

        var stagingDesc = Texture(targetWidth, targetHeight, 87u, UsageStaging, 0, CpuAccessRead);
        created = createTexture(devices.Device, ref stagingDesc, nint.Zero, out staging);
        return created < 0 ? $"CreateTexture2D refused the staging copy: 0x{created:X8}" : null;
    }

    /// <summary>
    /// Sends the vendor's switch, and reports <b>what it asked for</b> alongside what came back.
    /// The flag is read from the payload actually handed to the driver rather than from the
    /// parameter, so a caller that passes the same value twice cannot look like one that did not.
    /// </summary>
    private static int SetExtension(
        nint videoContext,
        nint processor,
        GpuVendor vendor,
        bool enable,
        out uint refusedFunction,
        out bool requested)
    {
        refusedFunction = 0;
        requested = false;
        var setStreamExtension = Com.Method<SetStreamExtensionFn>(videoContext, 39);
        switch (vendor)
        {
            case GpuVendor.Nvidia:
                var payload = D3d11UpscaleFormats.NvidiaStreamExtension(enable);

                // Read back out of the bytes on their way to the driver, not off the parameter:
                // what is recorded has to be what was sent.
                requested = payload[8] != 0;
                var guid = D3d11UpscaleFormats.NvidiaExtension;
                var pinned = GCHandle.Alloc(payload, GCHandleType.Pinned);
                try
                {
                    return setStreamExtension(
                        videoContext, processor, 0, ref guid, (uint)payload.Length, pinned.AddrOfPinnedObject());
                }
                finally
                {
                    pinned.Free();
                }

            case GpuVendor.Intel:
                var setOutputExtension = Com.Method<SetOutputExtensionFn>(videoContext, 19);
                var intelGuid = D3d11UpscaleFormats.IntelExtension;
                var last = 0;
                var intelCalls = D3d11UpscaleFormats.IntelSuperResolutionCalls(enable);

                // The scaling call is the one that means «super resolution»; the other two set up the
                // interface. Read from the calls themselves for the same reason as NVIDIA's.
                requested = intelCalls.Any(call => call.Function == 0x37u && call.Value != 0u);
                foreach (var call in intelCalls)
                {
                    // Intel's payload carries a POINTER to the parameter rather than the parameter,
                    // which is what a size of eight instead of sixteen gets wrong. Measured on this
                    // machine on 2026-09-12: passing the value inline is refused with E_INVALIDARG.
                    var parameter = new[] { call.Value };
                    var pinnedParameter = GCHandle.Alloc(parameter, GCHandleType.Pinned);
                    var vpeCall = new[]
                    {
                        new IntelVpeCall { Function = call.Function, Parameter = pinnedParameter.AddrOfPinnedObject() },
                    };
                    var pinnedPayload = GCHandle.Alloc(vpeCall, GCHandleType.Pinned);
                    try
                    {
                        var size = (uint)Marshal.SizeOf<IntelVpeCall>();
                        last = call.OnOutput
                            ? setOutputExtension(
                                videoContext, processor, ref intelGuid, size, pinnedPayload.AddrOfPinnedObject())
                            : setStreamExtension(
                                videoContext, processor, 0, ref intelGuid, size, pinnedPayload.AddrOfPinnedObject());
                        if (last < 0)
                        {
                            // Which of the three, because they mean different things: a refusal on
                            // the version call is an interface this driver does not have at all,
                            // while one on the scaling call is a driver that has it and will not do
                            // super resolution.
                            refusedFunction = call.Function;
                            return last;
                        }
                    }
                    finally
                    {
                        pinnedPayload.Free();
                        pinnedParameter.Free();
                    }
                }

                return last;

            default:
                // AMD asks through AMF and not through an extension, and no other vendor is known to
                // this probe. Reported as "not attempted" rather than as a failure.
                return 1;
        }
    }

    private static (byte[] Pixels, int Result) Draw(
        Devices devices,
        nint processor,
        nint inputView,
        nint outputView,
        nint output,
        nint staging,
        int targetWidth,
        int targetHeight)
    {
        var stream = new VideoProcessorStream { Enable = 1, InputSurface = inputView };
        var drawn = Com.Method<VideoProcessorBltFn>(devices.VideoContext, 53)(
            devices.VideoContext, processor, outputView, 0, 1, ref stream);

        Com.Method<CopyResourceFn>(devices.Context, 47)(devices.Context, staging, output);
        var mapped = new MappedSubresource();
        if (Com.Method<MapFn>(devices.Context, 14)(devices.Context, staging, 0, MapRead, 0, ref mapped) < 0)
        {
            return ([], drawn);
        }

        try
        {
            var pixels = new byte[targetWidth * targetHeight * 4];
            for (var row = 0; row < targetHeight; row++)
            {
                Marshal.Copy(mapped.Data + (row * (int)mapped.RowPitch), pixels, row * targetWidth * 4, targetWidth * 4);
            }

            return (pixels, drawn);
        }
        finally
        {
            Com.Method<UnmapFn>(devices.Context, 15)(devices.Context, staging, 0);
        }
    }

    private static Texture2DDesc Texture(int width, int height, uint format, uint usage, uint bind, uint cpuAccess) =>
        new()
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleCount = 1,
            SampleQuality = 0,
            Usage = usage,
            BindFlags = bind,
            CpuAccessFlags = cpuAccess,
            MiscFlags = 0,
        };

    private static AdapterUpscaleProbe Blocked(
        string description,
        uint vendorId,
        GpuVendor vendor,
        Dictionary<string, uint> support,
        string note,
        string? preferred = null) =>
        new(description, vendorId, vendor, support, preferred, null, null, null, note);

    private readonly record struct Devices(nint Device, nint Context, nint VideoDevice, nint VideoContext);

    private static class Com
    {
        public static T Method<T>(nint instance, int slot)
            where T : Delegate =>
            Marshal.GetDelegateForFunctionPointer<T>(
                Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * nint.Size));

        public static int QueryInterface(nint instance, Guid interfaceId, out nint result) =>
            Method<QueryInterfaceFn>(instance, 0)(instance, ref interfaceId, out result);

        public static void Release(nint instance)
        {
            if (instance != nint.Zero)
            {
                _ = Method<ReleaseFn>(instance, 2)(instance);
            }
        }
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid interfaceId, out nint factory);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        nint adapter,
        int driverType,
        nint software,
        uint flags,
        nint featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out nint device,
        out int featureLevel,
        out nint context);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceFn(nint self, ref Guid interfaceId, out nint result);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFn(nint self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Fn(nint self, uint index, out nint adapter);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDesc1Fn(nint self, ref AdapterDesc1 desc);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateVideoProcessorEnumeratorFn(
        nint self, ref VideoProcessorContentDesc desc, out nint enumerator);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CheckVideoProcessorFormatFn(nint self, uint format, out uint flags);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateVideoProcessorFn(nint self, nint enumerator, uint rateConversionIndex, out nint processor);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTexture2DFn(nint self, ref Texture2DDesc desc, nint initialData, out nint texture);

    /// <summary>The same slot as <see cref="CreateTexture2DFn"/>, for the call that uploads content.</summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTexture2DWithDataFn(
        nint self, ref Texture2DDesc desc, ref SubresourceData initialData, out nint texture);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateVideoProcessorInputViewFn(
        nint self, nint resource, nint enumerator, ref VideoProcessorInputViewDesc desc, out nint view);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateVideoProcessorOutputViewFn(
        nint self, nint resource, nint enumerator, ref VideoProcessorOutputViewDesc desc, out nint view);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void SetStreamColorSpaceFn(nint self, nint processor, uint streamIndex, ref uint colorSpace);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void SetOutputColorSpaceFn(nint self, nint processor, ref uint colorSpace);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetStreamExtensionFn(
        nint self, nint processor, uint streamIndex, ref Guid extensionId, uint dataSize, nint data);

    /// <summary>The output door, which takes no stream index. Slot 19, four before the stream one.</summary>
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetOutputExtensionFn(
        nint self, nint processor, ref Guid extensionId, uint dataSize, nint data);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetVideoProcessorCapsFn(nint self, ref VideoProcessorCaps caps);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetFilterRangeFn(nint self, uint filter, ref VideoProcessorFilterRange range);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void SetStreamFilterFn(
        nint self, nint processor, uint streamIndex, uint filter, int enabled, int level);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int VideoProcessorBltFn(
        nint self, nint processor, nint outputView, uint frame, uint streamCount, ref VideoProcessorStream streams);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void CopyResourceFn(nint self, nint destination, nint source);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int MapFn(nint self, nint resource, uint subresource, uint mapType, uint flags, ref MappedSubresource mapped);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void UnmapFn(nint self, nint resource, uint subresource);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct AdapterDesc1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint AdapterLuidLow;
        public int AdapterLuidHigh;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VideoProcessorContentDesc
    {
        public uint InputFrameFormat;
        public uint InputFrameRateNumerator;
        public uint InputFrameRateDenominator;
        public uint InputWidth;
        public uint InputHeight;
        public uint OutputFrameRateNumerator;
        public uint OutputFrameRateDenominator;
        public uint OutputWidth;
        public uint OutputHeight;
        public uint Usage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VideoProcessorInputViewDesc
    {
        public uint FourCc;
        public uint ViewDimension;
        public uint MipSlice;
        public uint ArraySlice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VideoProcessorOutputViewDesc
    {
        public uint ViewDimension;
        public uint MipSlice;
        public uint FirstArraySlice;
        public uint ArraySize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VideoProcessorStream
    {
        public int Enable;
        public uint OutputIndex;
        public uint InputFrameOrField;
        public uint PastFrames;
        public uint FutureFrames;
        public nint PastSurfaces;
        public nint InputSurface;
        public nint FutureSurfaces;
        public nint PastSurfacesRight;
        public nint InputSurfaceRight;
        public nint FutureSurfacesRight;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Texture2DDesc
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public uint SampleCount;
        public uint SampleQuality;
        public uint Usage;
        public uint BindFlags;
        public uint CpuAccessFlags;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SubresourceData
    {
        public nint SysMem;
        public uint SysMemPitch;
        public uint SysMemSlicePitch;
    }

    /// <summary>Nine unsigned numbers; the third is the filter bitmask this probe reads.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct VideoProcessorCaps
    {
        public uint DeviceCaps;
        public uint FeatureCaps;
        public uint FilterCaps;
        public uint InputFormatCaps;
        public uint AutoStreamCaps;
        public uint StereoCaps;
        public uint RateConversionCapsCount;
        public uint MaxInputStreams;
        public uint MaxStreamStates;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VideoProcessorFilterRange
    {
        public int Minimum;
        public int Maximum;
        public int Default;
        public float Multiplier;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        public nint Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    /// <summary>Intel's call: which function, and where its parameter lives.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct IntelVpeCall
    {
        public uint Function;
        public nint Parameter;
    }
}
