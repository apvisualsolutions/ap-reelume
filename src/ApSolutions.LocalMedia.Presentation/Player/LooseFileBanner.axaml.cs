// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace ApSolutions.LocalMedia.Presentation.Player;

public sealed partial class LooseFileBanner : UserControl
{
    public LooseFileBanner() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
