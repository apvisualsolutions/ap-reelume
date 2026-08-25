// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Catalog;

/// <summary>
/// The three title tools, in the card that opened the title rather than under the library.
/// </summary>
/// <remarks>
/// Two of the three are conditional, and the condition is a fact about the open title rather than
/// about this view — so each card says it. Editing what a title says is always possible; comparing
/// copies needs a second copy, and previewing a rename needs a rename that would change something.
/// Both surfaces behind those two answer with nothing otherwise, and the button stayed anyway: one
/// opened a comparison of a file against itself, the other a preview of no operations.
/// </remarks>
public sealed partial class TitleActionsView : UserControl
{
    /// <summary>Whether the open title has a second copy to compare against.</summary>
    public static readonly StyledProperty<bool> CanReviewVersionsProperty =
        AvaloniaProperty.Register<TitleActionsView, bool>(nameof(CanReviewVersions));

    /// <summary>Whether renaming the open title's file would change its name.</summary>
    public static readonly StyledProperty<bool> CanPreviewRenameProperty =
        AvaloniaProperty.Register<TitleActionsView, bool>(nameof(CanPreviewRename));

    public TitleActionsView()
    {
        InitializeComponent();
    }

    public bool CanReviewVersions
    {
        get => GetValue(CanReviewVersionsProperty);
        set => SetValue(CanReviewVersionsProperty, value);
    }

    public bool CanPreviewRename
    {
        get => GetValue(CanPreviewRenameProperty);
        set => SetValue(CanPreviewRenameProperty, value);
    }
}
