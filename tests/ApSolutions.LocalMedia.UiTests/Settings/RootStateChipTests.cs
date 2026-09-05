// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using ApSolutions.LocalMedia.Presentation.Settings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// A folder is in one of three states, and the colour has to say which one as well as the word.
/// </summary>
/// <remarks>
/// <para>
/// The prototype's <c>rootState()</c> returns «Conectada», «Desconectada» and «Acceso denegado» and
/// draws all three with the same tag element, so the red is the design's rather than an invention.
/// The application answers with three shapes: the green chip, the shared <c>UnavailableBadge</c>,
/// and the red chip that arrived on 2026-09-05 for a folder Windows refuses — telling somebody that
/// is «unavailable» sends them to look for a cable that is already plugged in.
/// </para>
/// <para>
/// <b>Why this exists, and it is not that the colours were doubted.</b> A style class that does not
/// exist does not fail: rename <c>denied</c> in the markup or in the dictionary and the badge quietly
/// keeps the family's neutral fill, saying «access denied» in the same colour as everything else,
/// and until today no test would have said a word. That is why the assertion is made against the
/// mounted view and not against a <c>Border</c> this file builds with the classes written by hand —
/// a hand-built control carries whatever classes the test gives it, so it would go on passing after
/// the view stopped asking for them.
/// </para>
/// <para>
/// The sibling green chip is measured in the same breath because it was equally uncovered, and
/// because what makes the pair readable is that they differ from the family fill <b>and</b> from
/// each other. <c>ContrastTokenTests</c> already proves those tokens contrast in the four visual
/// modes; what nothing proved is that these two badges are the ones wearing them.
/// </para>
/// </remarks>
public sealed class RootStateChipTests
{
    [AvaloniaFact]
    public async Task The_three_folder_states_wear_the_colour_the_design_gives_them()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var view = await MountAsync();

        // The anti-blindness floor is inside Chip(): the row template is instantiated once per
        // folder, so all three rows carry all three badges and only one of each is on screen. Chip()
        // fails both when none is showing and when more than one is.
        var connected = Chip(view, "RootConnectedBadge");
        var denied = Chip(view, "RootAccessDeniedBadge");

        Assert.Equal(ThemeColour("PositiveSurfaceBrush"), Fill(connected));
        Assert.Equal(ThemeColour("PositiveBorderBrush"), Stroke(connected));
        Assert.Equal(ThemeColour("DangerSurfaceBrush"), Fill(denied));
        Assert.Equal(ThemeColour("DangerBorderBrush"), Stroke(denied));

        // And the two assertions that catch the rename. Losing the modifier leaves the chip with the
        // family's own fill, which is a state chip that says nothing; losing the right one leaves
        // the two states saying the same thing in the same colour.
        Assert.NotEqual(ThemeColour("ControlFillBrush"), Fill(connected));
        Assert.NotEqual(ThemeColour("ControlFillBrush"), Fill(denied));
        Assert.NotEqual(Fill(connected), Fill(denied));
    }

    /// <summary>
    /// The refused folder wears one badge and not two, which is what the wrapper around the shared
    /// badge is for.
    /// </summary>
    /// <remarks>
    /// <c>UnavailableBadge</c> shows itself on <c>!IsAvailable</c>, and that is true of both
    /// failures. Without the panel that narrows it, a refused share would carry «no disponible» and
    /// «acceso denegado» at once — two sentences about one folder, one of them wrong.
    /// </remarks>
    [AvaloniaFact]
    public async Task A_refused_folder_is_not_also_called_unavailable()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));

        var view = await MountAsync();

        var shown = view.GetVisualDescendants()
            .OfType<ApSolutions.LocalMedia.Presentation.Library.UnavailableBadge>()
            .Count(badge => badge.IsEffectivelyVisible);

        // One disconnected folder is seeded, so exactly one shared badge is expected: fewer means
        // the state stopped being said at all, more means the refused folder took it too.
        Assert.Equal(1, shown);
    }

    private static async Task<RootManagementView> MountAsync()
    {
        var roots = new StubRoots(
            Root("D:\\Cine", RootAvailability.Available),
            Root("E:\\Respaldo", RootAvailability.Unavailable),
            Root("\\\\nas\\cine", RootAvailability.AccessDenied));

        var model = new RootOnboardingViewModel(
            new AddLibraryRoot(roots, new PassThroughNormalizer()),
            removeLibraryRoot: null,
            roots: roots);
        await model.RefreshRootsAsync();

        var view = new RootManagementView { DataContext = model };
        var window = new Window { Width = 1200, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, model.Roots.Count);
        return view;
    }

    /// <summary>
    /// The one badge of that name a person can actually see.
    /// </summary>
    /// <remarks>
    /// <b><c>IsVisible</c> is not «is seen», and believing it was cost the first red.</b> The row is
    /// a <c>DataTemplate</c>, so three folders mean three copies of every badge; and the shared
    /// unavailable badge shows itself on <c>!IsAvailable</c>, which is true of a refused folder too —
    /// it is a <c>Panel</c> above it that narrows it, and a control inside a hidden parent still
    /// reports <c>IsVisible = true</c>. <c>IsEffectivelyVisible</c> is the property that answers the
    /// question actually being asked, and it is the one the autonomous walk already uses.
    /// </remarks>
    private static Border Chip(RootManagementView view, string name) =>
        Assert.Single(
            view.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Name == name && border.IsEffectivelyVisible),
            border => border.Background is ISolidColorBrush);

    private static Color Fill(Border border) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(border.Background).Color;

    private static Color Stroke(Border border) =>
        Assert.IsAssignableFrom<ISolidColorBrush>(border.BorderBrush).Color;

    private static Color ThemeColour(string key)
    {
        var application = Avalonia.Application.Current;
        Assert.NotNull(application);
        Assert.True(
            application.TryGetResource(key, application.ActualThemeVariant, out var value),
            $"{key} is not declared in this theme variant.");
        return Assert.IsAssignableFrom<ISolidColorBrush>(value).Color;
    }

    private static LibraryRoot Root(string path, RootAvailability availability) => new(
        new LibraryRootId(Guid.NewGuid()),
        path,
        RootKind.Local,
        availability,
        ScanPolicy.Manual);

    private sealed class StubRoots(params LibraryRoot[] roots) : ILibraryRootRepository
    {
        private readonly List<LibraryRoot> _roots = [.. roots];

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roots.FirstOrDefault(root => root.Id == id));

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>(_roots);

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
