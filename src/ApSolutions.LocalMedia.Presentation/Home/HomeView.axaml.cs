// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ApSolutions.LocalMedia.Presentation.Home;

public sealed partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Continue is the primary action only when there is something to continue; otherwise the
        // library shortcut takes the focus, so the first key press always does something useful.
        Dispatcher.UIThread.Post(FocusPrimaryAction, DispatcherPriority.Loaded);
    }

    private void FocusPrimaryAction()
    {
        var target = DataContext is HomeViewModel { HasResume: true }
            ? ResumeHero.PrimaryAction
            : LibraryEntry.PrimaryAction;
        target.Focus();
    }
}
