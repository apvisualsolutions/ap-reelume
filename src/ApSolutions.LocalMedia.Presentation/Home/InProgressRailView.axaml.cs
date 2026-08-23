// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Home;

public sealed partial class InProgressRailView : UserControl
{
    public InProgressRailView()
    {
        InitializeComponent();
    }

    /// <summary>The library link, which takes the focus when there is nothing to continue.</summary>
    public Avalonia.Controls.Button PrimaryAction => LibraryEntryAction;
}
