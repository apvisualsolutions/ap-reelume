// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Startup;

/// <summary>
/// What the window holds while the database is being made ready.
/// </summary>
/// <remarks>
/// ARQ-005. It exists so the window can be shown in the first frame instead of after the work: the
/// preparation runs off the interface thread, and this is what stands in its place until the shell
/// or the recovery screen replaces it.
/// </remarks>
public sealed partial class StartupView : UserControl
{
    public StartupView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
