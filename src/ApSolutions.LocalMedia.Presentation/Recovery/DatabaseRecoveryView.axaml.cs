// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Recovery;

public sealed partial class DatabaseRecoveryView : UserControl
{
    public DatabaseRecoveryView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
