// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Onboarding;

/// <summary>
/// The floating question asked before a folder's catalogue is deleted, over the same
/// <see cref="RootOnboardingViewModel"/> that both the first run and the settings page use.
/// </summary>
public partial class RootRemoveDialog : UserControl
{
    public RootRemoveDialog()
    {
        InitializeComponent();
    }
}
