// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Theme;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Windows.Windowing;

public sealed class MicaBackdropService : IBackdropService
{
    public static string SolidFallbackResourceKey => "ShellSurfaceBrush";

    public bool TryApply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            window.TransparencyLevelHint = [WindowTransparencyLevel.None];
            return false;
        }

        window.TransparencyLevelHint =
            [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];
        return true;
    }
}
