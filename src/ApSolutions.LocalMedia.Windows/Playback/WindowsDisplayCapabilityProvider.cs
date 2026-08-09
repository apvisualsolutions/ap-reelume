using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Domain.Playback;

namespace ApSolutions.LocalMedia.Windows.Playback;

/// <summary>
/// Asks the Windows display configuration whether the active output supports and is currently using
/// advanced colour. When the query fails the answer is "no HDR": the application reports what it can
/// prove, never what it hopes.
/// </summary>
public sealed class WindowsDisplayCapabilityProvider : IDisplayCapabilityProvider
{
    private const uint QueryOnlyActivePaths = 0x00000002;

    // Windows 11 24H2 replaced the original advanced-colour query with a second version that
    // separates "wide colour" from "high dynamic range". The newer one is tried first and the older
    // one remains the fallback for earlier builds.
    private const int GetAdvancedColorInfo = 9;
    private const int GetAdvancedColorInfo2 = 13;
    private const uint AdvancedColorSupported = 0x1;
    private const uint AdvancedColorEnabled = 0x2;
    private const uint HighDynamicRangeSupported = 0x10;
    private const uint HighDynamicRangeUserEnabled = 0x20;

    /// <summary>
    /// Why the last query answered what it did. Recorded so an evidence run can tell "the display
    /// has no HDR" apart from "the query did not work on this machine".
    /// </summary>
    public string LastDiagnostic { get; private set; } = "not queried";

    public DisplayCapabilities GetCurrentDisplay()
    {
        try
        {
            var sizeResult = GetDisplayConfigBufferSizes(QueryOnlyActivePaths, out var pathCount, out var modeCount);
            if (sizeResult != 0)
            {
                LastDiagnostic = $"GetDisplayConfigBufferSizes returned {sizeResult}";
                return new DisplayCapabilities(SupportsHdr10: false, HdrEnabled: false);
            }

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];
            var queryResult = QueryDisplayConfig(
                QueryOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                nint.Zero);
            if (queryResult != 0)
            {
                LastDiagnostic = $"QueryDisplayConfig returned {queryResult}";
                return new DisplayCapabilities(SupportsHdr10: false, HdrEnabled: false);
            }

            var supported = false;
            var enabled = false;
            var queried = 0;
            var refused = 0;
            var lastError = 0;
            for (var index = 0; index < pathCount; index++)
            {
                var header = new DisplayConfigDeviceInfoHeader
                {
                    AdapterId = paths[index].TargetInfo.AdapterId,
                    Id = paths[index].TargetInfo.Id,
                };

                var modern = new DisplayConfigAdvancedColorInfo2
                {
                    Header = header with
                    {
                        Type = GetAdvancedColorInfo2,
                        Size = Marshal.SizeOf<DisplayConfigAdvancedColorInfo2>(),
                    },
                };
                var modernResult = DisplayConfigGetDeviceInfo2(ref modern);
                if (modernResult == 0)
                {
                    queried++;
                    supported |= (modern.Value & HighDynamicRangeSupported) != 0;
                    enabled |= (modern.Value & HighDynamicRangeUserEnabled) != 0;
                    continue;
                }

                var legacy = new DisplayConfigAdvancedColorInfo
                {
                    Header = header with
                    {
                        Type = GetAdvancedColorInfo,
                        Size = Marshal.SizeOf<DisplayConfigAdvancedColorInfo>(),
                    },
                };
                var legacyResult = DisplayConfigGetDeviceInfo(ref legacy);
                if (legacyResult != 0)
                {
                    refused++;
                    lastError = legacyResult;
                    continue;
                }

                queried++;
                supported |= (legacy.Value & AdvancedColorSupported) != 0;
                enabled |= (legacy.Value & AdvancedColorEnabled) != 0;
            }

            LastDiagnostic = $"paths={pathCount} queried={queried} refused={refused} lastError={lastError}";
            return new DisplayCapabilities(supported, enabled);
        }
        catch (DllNotFoundException)
        {
            return new DisplayCapabilities(SupportsHdr10: false, HdrEnabled: false);
        }
        catch (EntryPointNotFoundException)
        {
            return new DisplayCapabilities(SupportsHdr10: false, HdrEnabled: false);
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [Out] DisplayConfigPathInfo[] paths,
        ref uint modeCount,
        [Out] DisplayConfigModeInfo[] modes,
        nint currentTopology);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigAdvancedColorInfo requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetDeviceInfo2(ref DisplayConfigAdvancedColorInfo2 requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public int OutputTechnology;
        public int Rotation;
        public int Scaling;

        // DISPLAYCONFIG_RATIONAL is two 32-bit fields. Declaring it as one 64-bit field would make
        // the runtime align it and shift every field after it, which is why the query used to fail.
        public uint RefreshRateNumerator;
        public uint RefreshRateDenominator;
        public int ScanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)]
        public bool TargetAvailable;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo SourceInfo;
        public DisplayConfigPathTargetInfo TargetInfo;
        public uint Flags;
    }

    // infoType + id + adapterId + the 48-byte union of target, source, and desktop image modes.
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DisplayConfigModeInfo
    {
        public int InfoType;
        public uint Id;
        public Luid AdapterId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct DisplayConfigDeviceInfoHeader
    {
        public int Type;
        public int Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigAdvancedColorInfo
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public int ColorEncoding;
        public int BitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigAdvancedColorInfo2
    {
        public DisplayConfigDeviceInfoHeader Header;
        public uint Value;
        public int ActiveColorMode;
    }
}
