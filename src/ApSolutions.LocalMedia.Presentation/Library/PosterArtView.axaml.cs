// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia;
using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Library;

public sealed partial class PosterArtView : UserControl
{
    /// <summary>
    /// The title the picture is computed from.
    /// </summary>
    /// <remarks>
    /// A property of this view rather than its data context, and that is what lets one view serve
    /// all five surfaces: the hero's model calls it <c>ResumeTitle</c>, a card's calls it
    /// <c>Title</c>, and a detail banner's is the title itself. Binding the layers to the control's
    /// own property lets each host say which of its own values is the title, instead of every host
    /// having to shape its model to this view's expectations.
    /// </remarks>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PosterArtView, string?>(nameof(Title));

    public PosterArtView()
    {
        InitializeComponent();
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
