// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The notices strip takes space and pushes the grid down, and never covers it (ADR-0010).
/// </summary>
/// <remarks>
/// Geometry and not structure, for the reason <see cref="Player.LooseFileBannerPlacementTests"/>
/// already gave: a strip can be a sibling of the grid and still be painted across it. What is
/// asserted is that the grid actually moves — the same decision that band settled on 2026-08-21, and
/// the reason it has two geometric tests so it cannot quietly go back to being an overlay.
/// <para>
/// Without this the ADR would be a paragraph. The strip could be turned into an overlay tomorrow and
/// every other gate would stay green: the tokens are the same, the leading action is the same, and
/// nothing overflows either way.
/// </para>
/// </remarks>
public sealed class LibraryNoticesPlacementTests
{
    [AvaloniaFact]
    public void The_strip_pushes_the_grid_down_rather_than_covering_it()
    {
        var (window, view, viewModel) = Show();

        var surface = view.FindControl<StackPanel>("LibraryNoticesSurface");
        Assert.NotNull(surface);
        var filters = view.FindControl<WrapPanel>("LibraryFilterSurface");
        Assert.NotNull(filters);

        var quiet = filters!.TranslatePoint(new Point(0, 0), view);
        Assert.NotNull(quiet);
        Assert.Equal(0, surface!.Bounds.Height);

        // A root nobody can read, which is a STATE: it lasts as long as the condition does.
        viewModel.RootNotices.Apply(new RootAvailabilityChanged(
            new LibraryRootId(Guid.NewGuid()),
            "E:\\Respaldo",
            RootAvailability.Unavailable));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Bounds.Height > 0, "the strip measured nothing, so this proves nothing.");

        var pushed = filters.TranslatePoint(new Point(0, 0), view);
        Assert.NotNull(pushed);
        Assert.True(
            pushed!.Value.Y > quiet!.Value.Y,
            $"the filters start at y={pushed.Value.Y:F0} with a notice on screen and at "
                + $"y={quiet.Value.Y:F0} without one, so the strip is drawn over the grid instead of "
                + "pushing it.");

        window.Close();
    }

    /// <summary>
    /// And it goes back to nothing. A row that keeps its height once a notice has been and gone is a
    /// permanent gap, which is what the scan row was before the prototype's grammar was followed.
    /// </summary>
    [AvaloniaFact]
    public void The_row_measures_nothing_again_once_the_root_comes_back()
    {
        var (window, view, viewModel) = Show();
        var surface = view.FindControl<StackPanel>("LibraryNoticesSurface")!;
        var rootId = new LibraryRootId(Guid.NewGuid());

        viewModel.RootNotices.Apply(
            new RootAvailabilityChanged(rootId, "E:\\Respaldo", RootAvailability.AccessDenied));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();
        Assert.True(surface.Bounds.Height > 0);

        viewModel.RootNotices.Apply(
            new RootAvailabilityChanged(rootId, "E:\\Respaldo", RootAvailability.Available));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, surface.Bounds.Height);
        window.Close();
    }

    /// <summary>
    /// The scan is drawn two ways, and which one depends on who launched it (ADR-0010, point five).
    /// </summary>
    /// <remarks>
    /// The strip is checked by geometry rather than by the flag it binds to, because the flag is what
    /// the view model already asserts. What this adds is that the surface really is empty for the
    /// automatic case — a scan nobody asked for must not move anything.
    /// </remarks>
    [AvaloniaFact]
    public void A_scan_that_started_on_its_own_moves_nothing()
    {
        var (window, view, viewModel) = Show();
        var surface = view.FindControl<StackPanel>("LibraryNoticesSurface")!;
        var rootId = new LibraryRootId(Guid.NewGuid());

        viewModel.ScanProgress.Apply(new ScanProgressChanged(
            rootId, 120, 4, "E:\\Cine", IsCompleted: false, ScanTrigger.Watcher));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.ScanProgress.ShowsPulse, "an automatic scan gets the discreet mark.");
        Assert.Equal(0, surface.Bounds.Height);

        // And the same scan launched by hand does move it.
        viewModel.ScanProgress.Apply(new ScanProgressChanged(
            rootId, 121, 4, "E:\\Cine", IsCompleted: false, ScanTrigger.Manual));
        Dispatcher.UIThread.RunJobs();
        window.InvalidateMeasure();
        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Bounds.Height > 0, "a scan somebody launched may push, and this one did not.");
        window.Close();
    }

    private static (Window Window, LibraryView View, LibraryViewModel ViewModel) Show()
    {
        var viewModel = new LibraryViewModel(new EmptyCatalog());
        var view = new LibraryView { DataContext = viewModel };
        var window = new Window { Width = 1200, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view, viewModel);
    }

    private sealed class EmptyCatalog : Application.Catalog.ICatalogQueryService
    {
        public Task<Application.Catalog.CatalogPage> QueryAsync(
            Application.Catalog.CatalogQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Application.Catalog.CatalogPage([], null));
    }
}
