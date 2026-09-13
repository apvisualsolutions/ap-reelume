// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The question asked before a folder's catalogue is deleted. It says what will be lost, in numbers,
/// and it says the loss cannot be undone — which is what the owner decided on 2026-09-06 when the
/// prototype promised the catalogue would be kept and the application's own notice promised the
/// opposite, and the code did neither.
/// </summary>
public sealed class RootRemoveDialogTests
{
    [AvaloniaFact]
    public void The_dialog_is_bounded_in_both_dimensions_and_centred()
    {
        using var scope = Mount(WithSummary(new LibraryRootRemovalSummary(3, 5, TimeSpan.FromMinutes(92))));

        var panel = scope.Panel();

        // §4's overlay grammar. The panel that declared neither dimension was measured at 1280x1400,
        // opaque over the transport.
        Assert.Equal(520, panel.MaxWidth);
        Assert.Equal(560, panel.MaxHeight);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Center, panel.HorizontalAlignment);
        Assert.Equal(Avalonia.Layout.VerticalAlignment.Center, panel.VerticalAlignment);
        Assert.Equal(Brush("DangerSurfaceBrush"), panel.Background);
        Assert.Equal(Brush("DangerBorderBrush"), panel.BorderBrush);
    }

    [AvaloniaFact]
    public void The_dialog_states_the_counts_it_was_given()
    {
        using var scope = Mount(WithSummary(new LibraryRootRemovalSummary(3, 5, TimeSpan.FromMinutes(92))));

        // The numbers, inside the translated sentences and not beside them.
        Assert.Contains("3", scope.Text("RootRemoveSummaryTitles"), StringComparison.Ordinal);
        Assert.Contains("5", scope.Text("RootRemoveSummaryMarks"), StringComparison.Ordinal);
        Assert.Contains("92", scope.Text("RootRemoveSummaryMinutes"), StringComparison.Ordinal);

        // IsEffectivelyVisible and not IsVisible: the counts hang off a panel, so the block's own
        // flag stays true while the person sees nothing. What is measured is what is drawn.
        Assert.False(scope.Block("RootRemoveSummaryEmpty").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void The_dialog_warns_that_the_removal_cannot_be_undone()
    {
        using var scope = Mount(WithSummary(new LibraryRootRemovalSummary(3, 5, TimeSpan.FromMinutes(92))));

        // The whole reason the question is asked over everything instead of in a strip.
        Assert.Equal(
            Resource("RootRemoveIrreversibleNotice"),
            scope.Text("RootRemoveIrreversibleNotice"));
    }

    [AvaloniaFact]
    public void A_folder_with_nothing_to_lose_says_so_instead_of_showing_three_zeros()
    {
        using var scope = Mount(WithSummary(LibraryRootRemovalSummary.Empty));

        // A folder added a moment ago and not yet scanned. Three zeros read as a broken count.
        Assert.True(scope.Block("RootRemoveSummaryEmpty").IsEffectivelyVisible);
        Assert.False(scope.Block("RootRemoveSummaryTitles").IsEffectivelyVisible);
    }

    private static RootOnboardingViewModel WithSummary(LibraryRootRemovalSummary summary)
    {
        var roots = new StubRoots();
        var model = new RootOnboardingViewModel(
            new AddLibraryRoot(roots, new PassThroughNormalizer()),
            new RemoveLibraryRoot(roots),
            roots,
            new SummarizeLibraryRootRemoval(new StubReader(summary)));
        var row = new LibraryRootRowViewModel(new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            @"D:\Cine",
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual));
        model.RequestRemoveCommand.Execute(row);
        Dispatcher.UIThread.RunJobs();
        return model;
    }

    private static IBrush Brush(string key) => Assert.IsAssignableFrom<IBrush>(Resource(key));

    private static object Resource(string key)
    {
        var application = Avalonia.Application.Current!;
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared, so nothing can paint it.");
        Assert.NotNull(value);
        return value!;
    }

    private static Scope Mount(RootOnboardingViewModel model)
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        return new Scope(model);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Window _window;
        private readonly RootRemoveDialog _view;

        internal Scope(RootOnboardingViewModel model)
        {
            _view = new RootRemoveDialog { DataContext = model };
            _window = new Window { Width = 900, Height = 900, Content = _view };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        internal Border Panel() => Assert.Single(
            _view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "RootRemoveDialogPanel");

        internal TextBlock Block(string name) => Assert.Single(
            _view.GetVisualDescendants().OfType<TextBlock>(),
            block => block.Name == name);

        internal string Text(string name) => Block(name).Text ?? string.Empty;

        public void Dispose() => _window.Close();
    }

    private sealed class StubReader : ILibraryRootRemovalReader
    {
        private readonly LibraryRootRemovalSummary _summary;

        internal StubReader(LibraryRootRemovalSummary summary) => _summary = summary;

        public Task<LibraryRootRemovalSummary> SummarizeAsync(
            LibraryRootId id,
            CancellationToken cancellationToken = default) => Task.FromResult(_summary);
    }

    private sealed class StubRoots : ILibraryRootRepository
    {
        private readonly List<LibraryRoot> _roots = [];

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>([.. _roots]);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roots.Find(root => root.Id == id));

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default)
        {
            _roots.Add(root);
            return Task.CompletedTask;
        }

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TitleId>> RemoveAsync(
            LibraryRootId id,
            CancellationToken cancellationToken = default)
        {
            _roots.RemoveAll(root => root.Id == id);
            return Task.FromResult<IReadOnlyList<TitleId>>([]);
        }
    }

    private sealed class PassThroughNormalizer : IPathNormalizer
    {
        public string NormalizeAndValidate(string path, RootKind kind) => path;
    }
}
