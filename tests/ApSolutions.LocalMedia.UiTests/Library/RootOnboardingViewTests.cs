// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
/// The first screen anybody sees, in the four forms SURFACES lists for it.
/// </summary>
/// <remarks>
/// Three of the four were already here. The fourth - no roots at all - is how the screen starts and
/// it had nothing to paint: the heading and the rows were simply absent and nothing took their place.
/// </remarks>
public sealed class RootOnboardingViewTests
{
    private const string Filled = "\u25CF";
    private const string Hollow = "\u25CB";

    /// <summary>
    /// Pressing a kind changes the screen, which is what it did not do.
    /// </summary>
    /// <remarks>
    /// Nothing read <c>SelectedKind</c>: three buttons set it and no view painted it, so all three
    /// looked identical whichever was pressed. The field starts at <c>Local</c>, so there was not
    /// even an empty moment to make the absence obvious - which is why this asserts the circle moves
    /// rather than that one exists.
    /// </remarks>
    [AvaloniaFact]
    public void The_chosen_kind_is_the_only_one_wearing_the_filled_circle()
    {
        var model = Create();
        using var scope = Mount(model);

        Assert.Equal([Filled, Hollow, Hollow], scope.Cues());

        model.SelectedKind = RootKind.Usb;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([Hollow, Filled, Hollow], scope.Cues());

        model.SelectKindCommand.Execute(RootKind.Unc);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal([Hollow, Hollow, Filled], scope.Cues());
    }

    /// <summary>
    /// The three borders on this screen said three different things in one colour.
    /// </summary>
    /// <remarks>
    /// A refusal, a folder about to leave the catalog and a request for permission were all
    /// <c>AccentSubtleBrush</c>. Each is asserted to be the brush it should be <b>and</b> to differ
    /// from the accent it was, because comparing only against the right one passes just as well if
    /// somebody later makes them equal again.
    /// </remarks>
    [AvaloniaFact]
    public void A_refusal_a_removal_and_a_request_for_permission_no_longer_share_one_surface()
    {
        var accent = Brush("AccentSubtleBrush");
        using var scope = Mount(Create());

        var refusal = scope.Surface("RootAddFailureSurface");
        var removal = scope.Surface("RootRemoveConfirmationSurface");

        Assert.Equal(Brush("WarningSurfaceBrush"), refusal.Background);
        Assert.Equal(Brush("WarningBorderBrush"), refusal.BorderBrush);
        Assert.NotEqual(accent, refusal.Background);

        Assert.Equal(Brush("DangerSurfaceBrush"), removal.Background);
        Assert.Equal(Brush("DangerBorderBrush"), removal.BorderBrush);
        Assert.NotEqual(accent, removal.Background);

        Assert.NotEqual(refusal.Background, removal.Background);
    }

    /// <summary>
    /// The empty catalog says so, and the list takes its place rather than sitting beside it.
    /// </summary>
    /// <remarks>
    /// Both halves are asserted: an empty state that stays on screen under a full list says the
    /// wrong thing twice, which is the failure the review inbox's own empty state was written to
    /// avoid.
    /// </remarks>
    [AvaloniaFact]
    public async Task An_empty_library_says_so_and_the_list_takes_its_place()
    {
        var roots = new StubRoots();
        var model = CreateWith(roots);
        using var scope = Mount(model);

        await model.RefreshRootsAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.True(model.HasNoRoots);
        Assert.True(scope.Surface("RootListEmptySurface").IsVisible);

        roots.Add(Existing("R:\\media"));
        await model.RefreshRootsAsync(TestContext.Current.CancellationToken);
        Dispatcher.UIThread.RunJobs();
        Assert.False(model.HasNoRoots);
        Assert.False(scope.Surface("RootListEmptySurface").IsVisible);
    }

    /// <summary>
    /// The label above the path box is painted and not only announced.
    /// </summary>
    /// <remarks>
    /// <c>RootPathLabel</c> was written in both languages and spent by <c>AutomationProperties.Name</c>
    /// alone, so a screen reader said "folder path" and the screen said nothing. It is compared
    /// against the resource rather than against a sentence, so this file is not a third place the
    /// same words are written.
    /// </remarks>
    [AvaloniaFact]
    public void The_path_box_carries_the_label_it_only_announced()
    {
        using var scope = Mount(Create());

        var label = Assert.IsType<string>(Resource("RootPathLabel"));
        Assert.Contains(scope.Blocks(), block => string.Equals(block.Text, label, StringComparison.Ordinal));
    }

    private static RootOnboardingViewModel Create() => CreateWith(new StubRoots());

    private static RootOnboardingViewModel CreateWith(StubRoots roots) => new(
        new AddLibraryRoot(roots, new PassThroughNormalizer()),
        new RemoveLibraryRoot(roots),
        roots);

    private static LibraryRoot Existing(string path) => new(
        new LibraryRootId(Guid.NewGuid()),
        path,
        RootKind.Local,
        RootAvailability.Available,
        ScanPolicy.Manual);

    private static IBrush Brush(string key) => Assert.IsAssignableFrom<IBrush>(Resource(key));

    /// <summary>
    /// Asked for with the theme variant, because the brushes live in theme dictionaries.
    /// </summary>
    /// <remarks>
    /// <c>TryFindResource</c> finds the scalars and finds none of the brushes: a brush is declared
    /// four times over, once per variant, and reaching it needs to say which one.
    /// </remarks>
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
        private readonly RootOnboardingView _view;

        internal Scope(RootOnboardingViewModel model)
        {
            _view = new RootOnboardingView { DataContext = model };
            _window = new Window { Width = 900, Height = 900, Content = _view };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>The three kind circles, in the order the markup declares them.</summary>
        internal string[] Cues() =>
        [
            .. Blocks().Where(block => block.Classes.Contains("state-glyph")).Select(block => block.Text ?? string.Empty),
        ];

        internal TextBlock[] Blocks() => [.. _view.GetVisualDescendants().OfType<TextBlock>()];

        internal Border Surface(string name)
        {
            var border = _view.GetVisualDescendants().OfType<Border>().FirstOrDefault(
                candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
            Assert.True(border is not null, $"{name} is not in the tree, so nothing about it can be asserted.");
            return border!;
        }

        public void Dispose() => _window.Close();
    }

    private sealed class StubRoots : ILibraryRootRepository
    {
        private readonly List<LibraryRoot> _roots = [];

        internal void Add(LibraryRoot root) => _roots.Add(root);

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roots.FirstOrDefault(root => root.Id == id));

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>([.. _roots]);

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default)
        {
            _roots.Add(root);
            return Task.CompletedTask;
        }

        public Task SetAvailabilityAsync(
            LibraryRootId id,
            RootAvailability availability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default)
        {
            _roots.RemoveAll(root => root.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughNormalizer : IPathNormalizer
    {
        public string NormalizeAndValidate(string path, RootKind kind) => path;
    }
}
