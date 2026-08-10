// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Home;

public sealed partial class ResumeHeroView : UserControl
{
    public ResumeHeroView()
    {
        InitializeComponent();
    }

    /// <summary>The Continue button, so Home can make it the first focus when it is offered.</summary>
    public Button PrimaryAction => ResumeHeroAction;
}
