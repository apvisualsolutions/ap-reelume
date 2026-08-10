// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Reflection;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Library;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

public sealed class LibraryNavigationTests
{
    [Fact]
    public void Library_slice_owns_browse_item_details_and_navigation_state_types()
    {
        var assembly = Assembly.Load("ApSolutions.LocalMedia.Presentation");

        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Presentation.Library.LibraryView",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Presentation.Library.LibraryViewModel",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Presentation.Library.CatalogItemViewModel",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Presentation.Movie.MovieDetailsView",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Presentation.Show.ShowDetailsView",
            throwOnError: false));
    }

    [Fact]
    public void Shell_exposes_the_library_surface_on_the_approved_library_route()
    {
        var type = Assembly.Load("ApSolutions.LocalMedia.Presentation").GetType(
            "ApSolutions.LocalMedia.Presentation.Shell.ShellViewModel",
            throwOnError: true);

        Assert.NotNull(type);
        Assert.NotNull(type.GetProperty("Library"));
        Assert.NotNull(type.GetProperty("IsLibraryVisible"));
    }

    [Fact]
    public async Task Browse_details_back_preserves_query_items_cursor_and_scroll_anchor()
    {
        var movie = Item(1, CatalogTitleKind.Movie, "Arrival");
        var show = Item(2, CatalogTitleKind.Show, "Dark");
        var queryService = new RecordingQueryService(
            new CatalogPage([movie], "next-page"),
            new CatalogPage([show], null));
        var viewModel = new LibraryViewModel(queryService)
        {
            Search = "ciencia",
            Filters = CatalogFilter.Available,
            Sort = CatalogSort.Year,
        };

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
        var first = viewModel.Items[0];
        viewModel.OpenDetails(first);

        Assert.Equal(LibrarySurface.MovieDetails, viewModel.Surface);
        Assert.Equal(first, viewModel.SelectedItem);
        Assert.Equal(first.Item.Id, viewModel.ScrollAnchorId);
        Assert.Equal("ciencia", viewModel.Search);
        Assert.Equal(CatalogFilter.Available, viewModel.Filters);
        Assert.Equal(CatalogSort.Year, viewModel.Sort);
        Assert.Equal(["Arrival", "Dark"], viewModel.Items.Select(item => item.Title));
        Assert.Equal("next-page", queryService.Queries[1].Cursor);

        viewModel.BackToLibrary();
        Assert.Equal(LibrarySurface.Browse, viewModel.Surface);
        Assert.Equal(first.Item.Id, viewModel.ScrollAnchorId);
        Assert.Equal(["Arrival", "Dark"], viewModel.Items.Select(item => item.Title));

        viewModel.OpenDetails(viewModel.Items[1]);
        Assert.Equal(LibrarySurface.ShowDetails, viewModel.Surface);
    }

    [AvaloniaFact]
    public async Task Library_renders_virtualized_bilingual_snapshots()
    {
        var queryService = new RecordingQueryService(new CatalogPage(
            [Item(3, CatalogTitleKind.Movie, "La llegada")],
            null));
        var viewModel = new LibraryViewModel(queryService);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var view = new LibraryView { DataContext = viewModel };
            var window = new Window
            {
                Width = 1024,
                Height = 720,
                Content = view,
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var artifactPath = Path.Combine(
                GetRepositoryRoot(),
                "artifacts",
                "ui-captures",
                "T7",
                $"library-{cultureName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
            Assert.True(File.Exists(artifactPath));
            window.Close();
        }
    }

    private static CatalogItem Item(int seed, CatalogTitleKind kind, string title) => new(
        new TitleId(CreateGuid(seed)),
        kind,
        title,
        2000 + seed,
        IsAvailable: true,
        HasProgress: false,
        IsPersonal: false,
        DateTimeOffset.UnixEpoch.AddDays(seed),
        LastPlayedUtc: null);

    private static Guid CreateGuid(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new Guid(bytes);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private sealed class RecordingQueryService(params CatalogPage[] pages) : ICatalogQueryService
    {
        private int _index;

        public List<CatalogQuery> Queries { get; } = [];

        public Task<CatalogPage> QueryAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Queries.Add(query);
            var page = pages[Math.Min(_index, pages.Length - 1)];
            _index++;
            return Task.FromResult(page);
        }
    }
}
