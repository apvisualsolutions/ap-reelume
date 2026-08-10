// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Runtime.InteropServices;
using ApSolutions.LocalMedia.Presentation.Theme;

namespace ApSolutions.LocalMedia.Windows.Accessibility;

public sealed class WindowsReducedMotionService : IReducedMotionService
{
    private const uint GetClientAreaAnimation = 0x1042;

    public bool IsEnabled
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            return SystemParametersInfo(
                GetClientAreaAnimation,
                uiParam: 0,
                out var animationsEnabled,
                fWinIni: 0)
                && !animationsEnabled;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint uiAction,
        uint uiParam,
        [MarshalAs(UnmanagedType.Bool)] out bool pvParam,
        uint fWinIni);
}
