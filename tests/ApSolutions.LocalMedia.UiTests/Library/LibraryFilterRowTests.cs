// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Library;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// The filter row's model: the kind pills over their own two bits, the drop-downs that apply as
/// they are chosen, and the one press that puts the whole row back.
/// </summary>
/// <remarks>
/// These are the branches the redesign added, asserted where they live. The kind and the status
/// write disjoint bits of one flags value — the repository always combined them, and a single
/// control bound to the whole value is what made them exclusive on screen — so the tests here are
/// about the seams: a pill press must not clear a status, a status must not clear a kind, and
/// nothing may query before anything has loaded.
/// </remarks>
public sealed class LibraryFilterRowTests
{
    [Fact]
    public async Task Kind_and_status_write_disjoint_bits_and_survive_each_other()
    {
        var service = new RecordingQueryService(new CatalogPage([], null));
        var viewModel = new LibraryViewModel(service);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.StatusFilter = CatalogFilter.Available;
        Assert.Equal(CatalogFilter.Available, viewModel.Filters);

        viewModel.TypeFilter = CatalogFilter.Movie;
        Assert.Equal(CatalogFilter.Movie | CatalogFilter.Available, viewModel.Filters);
        Assert.Equal(CatalogFilter.Movie, viewModel.TypeFilter);
        Assert.Equal(CatalogFilter.Available, viewModel.StatusFilter);

        // "Films I have not started" stays expressible: the status moves and the kind stands.
        viewModel.StatusFilter = CatalogFilter.Progress;
        Assert.Equal(CatalogFilter.Movie | CatalogFilter.Progress, viewModel.Filters);
        Assert.Equal(CatalogFilter.Movie, viewModel.TypeFilter);
    }

    [Fact]
    public async Task The_pills_query_as_they_are_pressed_and_refuse_a_parameter_that_is_not_a_kind()
    {
        var service = new RecordingQueryService(new CatalogPage([], null));
        var viewModel = new LibraryViewModel(service);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.SelectTypeCommand.CanExecute(null));
        Assert.True(viewModel.SelectTypeCommand.CanExecute(CatalogFilter.Show));

        viewModel.SelectTypeCommand.Execute(CatalogFilter.Show);
        await WaitForQueriesAsync(service, 2);
        Assert.Equal(CatalogFilter.Show, service.Queries[^1].Filters);
    }

    [Fact]
    public async Task The_drop_downs_apply_on_pick_but_only_once_something_has_loaded()
    {
        var service = new RecordingQueryService(new CatalogPage([], null));
        var viewModel = new LibraryViewModel(service);

        // Before the first load there is nothing to re-run: a constructor initializer that queried
        // would eat a page the host was about to ask for.
        viewModel.StatusFilter = CatalogFilter.Available;
        viewModel.Sort = CatalogSort.Year;
        Assert.Empty(service.Queries);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Single(service.Queries);

        viewModel.StatusFilter = CatalogFilter.Progress;
        await WaitForQueriesAsync(service, 2);

        // The same value again is a no-op, which is what lets the binding push the current value
        // back at attach without a query.
        viewModel.StatusFilter = CatalogFilter.Progress;
        Assert.Equal(2, service.Queries.Count);

        viewModel.Sort = CatalogSort.Added;
        await WaitForQueriesAsync(service, 3);
        Assert.Equal(CatalogSort.Added, service.Queries[^1].Sort);
    }

    [Fact]
    public void Each_pill_cues_its_own_selection_and_Todo_covers_both_spellings_of_everything()
    {
        var viewModel = new LibraryViewModel(new RecordingQueryService(new CatalogPage([], null)));

        Assert.True(viewModel.IsEveryKind);
        Assert.Equal("●", viewModel.EveryKindStateCue);
        Assert.Equal("○", viewModel.MoviesOnlyStateCue);
        Assert.Equal("○", viewModel.ShowsOnlyStateCue);

        viewModel.TypeFilter = CatalogFilter.Movie;
        Assert.True(viewModel.IsMoviesOnly);
        Assert.False(viewModel.IsEveryKind);
        Assert.Equal("●", viewModel.MoviesOnlyStateCue);

        viewModel.TypeFilter = CatalogFilter.Show;
        Assert.True(viewModel.IsShowsOnly);
        Assert.Equal("●", viewModel.ShowsOnlyStateCue);

        // Both kind bits at once is "everything" too: the mask spelling of Todo.
        viewModel.TypeFilter = CatalogFilter.Movie | CatalogFilter.Show;
        Assert.True(viewModel.IsEveryKind);
    }

    [Fact]
    public async Task An_empty_grid_is_the_library_or_the_search_and_never_both()
    {
        var service = new RecordingQueryService(new CatalogPage([], null));
        var viewModel = new LibraryViewModel(service);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        // Nothing narrowing: the library itself is empty.
        Assert.True(viewModel.IsLibraryEmpty);
        Assert.False(viewModel.IsSearchWithoutResults);

        // A search that found nothing is an answer about the search.
        viewModel.Search = "Zzz";
        Assert.False(viewModel.IsLibraryEmpty);
        Assert.True(viewModel.IsSearchWithoutResults);
    }

    [Fact]
    public async Task Clear_filters_puts_the_whole_row_back_with_one_query()
    {
        var service = new RecordingQueryService(new CatalogPage([], null));
        var viewModel = new LibraryViewModel(service);
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(viewModel.IsFiltersDirty);

        viewModel.Search = "ciencia";
        viewModel.TypeFilter = CatalogFilter.Movie;
        viewModel.StatusFilter = CatalogFilter.Available;
        viewModel.Sort = CatalogSort.Year;
        Assert.True(viewModel.IsFiltersDirty);
        await WaitForQueriesAsync(service, 3);
        var before = service.Queries.Count;

        viewModel.ClearFiltersCommand.Execute(null);
        await WaitForQueriesAsync(service, before + 1);

        Assert.Equal(before + 1, service.Queries.Count);
        Assert.True(string.IsNullOrEmpty(viewModel.Search));
        Assert.Equal(CatalogFilter.None, viewModel.Filters);
        Assert.Equal(CatalogSort.Title, viewModel.Sort);
        Assert.False(viewModel.IsFiltersDirty);
        var last = service.Queries[^1];
        Assert.Null(last.Search);
        Assert.Equal(CatalogFilter.None, last.Filters);
        Assert.Equal(CatalogSort.Title, last.Sort);
    }

    [Fact]
    public void The_header_counts_what_the_grid_holds_and_the_add_command_is_the_hosts_to_wire()
    {
        var viewModel = new LibraryViewModel(new RecordingQueryService(new CatalogPage([], null)));
        Assert.Equal(0, viewModel.ItemCount);
        Assert.Null(viewModel.AddMediaCommand);

        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, args) => raised.Add(args.PropertyName);
        viewModel.AddMediaCommand = viewModel.RefreshCommand;
        Assert.Same(viewModel.RefreshCommand, viewModel.AddMediaCommand);
        Assert.Contains(nameof(LibraryViewModel.AddMediaCommand), raised);
    }

    /// <summary>
    /// The commands run their query through an async void the test cannot await, so the probe is
    /// the recorded query count — the same thing the walk polls for.
    /// </summary>
    private static async Task WaitForQueriesAsync(RecordingQueryService service, int expected)
    {
        for (var attempt = 0; attempt < 200 && service.Queries.Count < expected; attempt++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, service.Queries.Count);
    }

    /// <summary>
    /// The skeleton stands only during the first query of an empty grid, and never over cards
    /// somebody is already reading: a reload keeps the content and loads behind it.
    /// </summary>
    [Fact]
    public async Task The_skeleton_stands_while_the_first_query_runs_and_never_over_content()
    {
        var gate = new TaskCompletionSource<CatalogPage>();
        var viewModel = new LibraryViewModel(new GatedQueryService(gate.Task));
        Assert.False(viewModel.ShowsSkeleton);

        var announced = new List<string>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        var loading = viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsLoading);
        Assert.True(viewModel.ShowsSkeleton);

        gate.SetResult(new CatalogPage([Card("Arrival")], null));
        await loading;

        Assert.False(viewModel.IsLoading);
        Assert.False(viewModel.ShowsSkeleton);
        Assert.Contains(nameof(viewModel.ShowsSkeleton), announced);

        // The reload: cards on screen, the query running again, and no skeleton over them.
        var second = new TaskCompletionSource<CatalogPage>();
        typeof(LibraryViewModel).GetField("_queryService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(viewModel, new GatedQueryService(second.Task));
        var reloading = viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsLoading);
        Assert.False(viewModel.ShowsSkeleton);
        second.SetResult(new CatalogPage([Card("Arrival")], null));
        await reloading;
    }

    /// <summary>
    /// Two loads in flight at once - a double press on a refresh - and the second finds the flag
    /// already raised: the set that changes nothing says nothing, which is the announcement side
    /// CI measured as never taken.
    /// </summary>
    [Fact]
    public async Task Overlapping_loads_raise_the_flag_once_and_the_second_set_stays_quiet()
    {
        var gate = new TaskCompletionSource<CatalogPage>();
        var viewModel = new LibraryViewModel(new GatedQueryService(gate.Task));

        var first = viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsLoading);

        var announced = new List<string>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);
        var second = viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(nameof(viewModel.ShowsSkeleton), announced);

        gate.SetResult(new CatalogPage([Card("Arrival")], null));
        await first;
        await second;
        Assert.False(viewModel.IsLoading);
    }

    private static CatalogItem Card(string title) => new(
        new TitleId(Guid.NewGuid()),
        CatalogTitleKind.Movie,
        title,
        2016,
        IsAvailable: true,
        HasProgress: false,
        IsPersonal: false,
        DateTimeOffset.UnixEpoch,
        LastPlayedUtc: null);

    private sealed class GatedQueryService(Task<CatalogPage> answer) : ICatalogQueryService
    {
        public Task<CatalogPage> QueryAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default) => answer;
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
