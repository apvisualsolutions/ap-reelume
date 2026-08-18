// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Presentation.Theme;

namespace ApSolutions.LocalMedia.Windows.Accessibility;

/// <summary>
/// Asks Windows the two questions <see cref="HighContrastPolicy"/> answers: the accessibility flags
/// word, and the colour the system draws windows with.
/// </summary>
/// <remarks>
/// This type only asks. Neither call's result is branched on here — what a flags word or a colour
/// means is policy, and policy lives where it can be measured without a machine that happens to be
/// in high contrast. A failed <c>SystemParametersInfo</c> leaves the structure as it was initialised,
/// so its flags read zero and the answer is "no high contrast", which is the right answer to give
/// when the system did not give one.
/// </remarks>
public sealed class WindowsHighContrastService : IHighContrastService
{
    private const uint GetHighContrast = 0x0042;
    private const int ColorWindow = 5;

    public bool IsEnabled
    {
        get
        {
            var info = new HighContrastInfo { Size = (uint)Marshal.SizeOf<HighContrastInfo>() };
            _ = SystemParametersInfo(GetHighContrast, info.Size, ref info, fWinIni: 0);
            return HighContrastPolicy.IsOn(info.Flags);
        }
    }

    public bool IsLight => HighContrastPolicy.IsLight(GetSysColor(ColorWindow));

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        ref HighContrastInfo pvParam,
        uint fWinIni);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetSysColor(int nIndex);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HighContrastInfo
    {
        public uint Size;
        public uint Flags;
        public nint DefaultScheme;
    }
}
