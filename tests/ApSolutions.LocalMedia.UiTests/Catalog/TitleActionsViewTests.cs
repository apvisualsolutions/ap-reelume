// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Catalog;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Catalog;

/// <summary>
/// Two of the three title tools are conditional, and the condition is a fact about the open title.
/// </summary>
/// <remarks>
/// Both used to be there always: one opened a comparison of a file against itself, the other a
/// preview of no operations. The properties that carry the two facts arrived with the view and were
/// read by nobody — a card said what it knew and nothing asked, which is the shape this repository
/// keeps finding. What is asserted is that the flag reaches the button, because a property that only
/// stores is the same defect wearing a different hat.
/// </remarks>
public sealed class TitleActionsViewTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Each_conditional_tool_is_shown_exactly_when_its_fact_is_true(
        bool canPreviewRename,
        bool canReviewVersions)
    {
        var view = new TitleActionsView
        {
            CanPreviewRename = canPreviewRename,
            CanReviewVersions = canReviewVersions,
        };
        var window = new Window { Content = view, Width = 900, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(canPreviewRename, view.CanPreviewRename);
        Assert.Equal(canReviewVersions, view.CanReviewVersions);

        var buttons = view.GetLogicalDescendants().OfType<Button>().ToArray();
        Assert.Equal(3, buttons.Length);

        // Editing what a title says is always possible; the other two answer to their own fact.
        Assert.True(buttons[0].IsVisible);
        Assert.Equal(canPreviewRename, buttons[1].IsVisible);
        Assert.Equal(canReviewVersions, buttons[2].IsVisible);
        window.Close();
    }
}
