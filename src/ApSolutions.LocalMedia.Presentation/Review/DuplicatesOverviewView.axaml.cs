// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Review;

/// <summary>
/// The duplicates destination: the list of titles that resolve to more than one file, each row
/// opening the comparison the review draws.
/// </summary>
public partial class DuplicatesOverviewView : UserControl
{
    public DuplicatesOverviewView()
    {
        InitializeComponent();
    }
}
