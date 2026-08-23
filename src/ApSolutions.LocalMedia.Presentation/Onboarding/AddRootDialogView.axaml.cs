// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Onboarding;

/// <summary>
/// The floating «Añadir raíz de medios» panel: the add-root form in the frame the prototype gives
/// it, over the same <see cref="RootOnboardingViewModel"/> the first run uses inline.
/// </summary>
public partial class AddRootDialogView : UserControl
{
    public AddRootDialogView()
    {
        InitializeComponent();
    }
}
