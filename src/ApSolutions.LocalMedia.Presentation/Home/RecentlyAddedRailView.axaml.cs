// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Home;

public sealed partial class RecentlyAddedRailView : UserControl
{
    public RecentlyAddedRailView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The library link, which takes the focus when there is nothing to continue.
    /// </summary>
    /// <remarks>
    /// It used to live on the rail above. The prototype writes it at the right of this heading, so
    /// it moved here on 2026-08-25 and the focus fallback moved with it: a fallback pointing at a
    /// control that is not on the surface any more is the way a keyboard lands nowhere.
    /// </remarks>
    public Avalonia.Controls.Button PrimaryAction => LibraryEntryAction;
}
