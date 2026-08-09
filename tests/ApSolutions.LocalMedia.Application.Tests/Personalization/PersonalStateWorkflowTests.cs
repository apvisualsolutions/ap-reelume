using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Personalization;

/// <summary>
/// Setting, clearing, filtering, and surviving a randomized workload. Nothing in this workflow reaches
/// the network, and nothing creates a profile or a list.
/// </summary>
public sealed class PersonalStateWorkflowTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Marking_and_unmarking_a_favorite_is_idempotent_and_stores_one_row()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));
        var content = Content(1);

        var first = await command.SetFavoriteAsync(content, true, TestContext.Current.CancellationToken);
        var second = await command.SetFavoriteAsync(content, true, TestContext.Current.CancellationToken);

        Assert.True(first.IsFavorite);
        Assert.True(second.IsFavorite);
        Assert.Single(await repository.GetAllAsync(TestContext.Current.CancellationToken));

        var cleared = await command.SetFavoriteAsync(content, false, TestContext.Current.CancellationToken);
        Assert.False(cleared.IsFavorite);
        Assert.Empty(await repository.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Watch_later_and_rating_are_stored_independently_of_the_favorite()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));
        var content = Content(2);

        await command.SetFavoriteAsync(content, true, TestContext.Current.CancellationToken);
        await command.SetWatchLaterAsync(content, true, TestContext.Current.CancellationToken);
        var rated = await command.SetRatingAsync(content, 7, TestContext.Current.CancellationToken);

        Assert.True(rated.IsFavorite);
        Assert.True(rated.IsWatchLater);
        Assert.Equal(7, rated.Rating);

        var unfavorited = await command.SetFavoriteAsync(content, false, TestContext.Current.CancellationToken);
        Assert.False(unfavorited.IsFavorite);
        Assert.True(unfavorited.IsWatchLater);
        Assert.Equal(7, unfavorited.Rating);
    }

    [Fact]
    public async Task A_rating_outside_the_range_is_refused_and_nothing_is_written()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => command.SetRatingAsync(Content(3), 0, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => command.SetRatingAsync(Content(3), 11, TestContext.Current.CancellationToken));

        Assert.Empty(await repository.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Toggling_reads_what_is_stored_rather_than_assuming_a_starting_value()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));
        var content = Content(4);
        await repository.SaveAsync(
            PersonalState.Empty(content).WithFavorite(true),
            Noon,
            TestContext.Current.CancellationToken);

        var toggled = await command.ToggleFavoriteAsync(content, TestContext.Current.CancellationToken);

        Assert.False(toggled.IsFavorite);
        Assert.True((await command.ToggleWatchLaterAsync(content, TestContext.Current.CancellationToken))
            .IsWatchLater);
    }

    [Fact]
    public async Task The_last_mark_removed_drops_the_row_instead_of_leaving_an_empty_one()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));
        var content = Content(5);

        await command.SetRatingAsync(content, 4, TestContext.Current.CancellationToken);
        Assert.Single(await repository.GetAllAsync(TestContext.Current.CancellationToken));

        await command.SetRatingAsync(content, null, TestContext.Current.CancellationToken);
        Assert.Empty(await repository.GetAllAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reading_content_that_was_never_marked_returns_an_empty_state_not_a_failure()
    {
        var repository = new MemoryPersonalStateRepository();
        var query = new GetPersonalFilters(repository);

        var state = await query.GetAsync(Content(6), TestContext.Current.CancellationToken);

        Assert.True(state.IsEmpty);
        Assert.Equal(Content(6), state.Content);
        Assert.Empty(await query.GetMarkedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_query_translates_marks_into_the_catalog_filters()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));
        await command.SetFavoriteAsync(Content(7), true, TestContext.Current.CancellationToken);
        await command.SetWatchLaterAsync(Content(8), true, TestContext.Current.CancellationToken);
        await command.SetRatingAsync(Content(9), 9, TestContext.Current.CancellationToken);
        var query = new GetPersonalFilters(repository);

        var marked = await query.GetMarkedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, marked.Count);
        Assert.Equal(
            CatalogFilter.Favorite,
            GetPersonalFilters.ToFilter(favorites: true, watchLater: false, rated: false));
        Assert.Equal(
            CatalogFilter.WatchLater | CatalogFilter.Rated,
            GetPersonalFilters.ToFilter(favorites: false, watchLater: true, rated: true));
        Assert.Equal(
            CatalogFilter.None,
            GetPersonalFilters.ToFilter(favorites: false, watchLater: false, rated: false));
    }

    [Fact]
    public async Task A_thousand_random_changes_end_where_a_reference_model_says_they_should()
    {
        var repository = new MemoryPersonalStateRepository();
        var command = new SetPersonalState(repository, new FixedClock(Noon));
        var reference = new Dictionary<string, ReferenceMarks>(StringComparer.Ordinal);
        var random = new Random(20260803);

        for (var index = 0; index < 1_000; index++)
        {
            var content = Content(random.Next(1, 40));
            var key = content.Value;
            var current = reference.GetValueOrDefault(key, new ReferenceMarks(false, false, null));
            switch (random.Next(5))
            {
                case 0:
                    var favorite = random.Next(2) == 1;
                    await command.SetFavoriteAsync(content, favorite, TestContext.Current.CancellationToken);
                    reference[key] = current with { Favorite = favorite };
                    break;
                case 1:
                    var watchLater = random.Next(2) == 1;
                    await command.SetWatchLaterAsync(content, watchLater, TestContext.Current.CancellationToken);
                    reference[key] = current with { WatchLater = watchLater };
                    break;
                case 2:
                    var rating = random.Next(1, 11);
                    await command.SetRatingAsync(content, rating, TestContext.Current.CancellationToken);
                    reference[key] = current with { Rating = rating };
                    break;
                case 3:
                    await command.SetRatingAsync(content, null, TestContext.Current.CancellationToken);
                    reference[key] = current with { Rating = null };
                    break;
                default:
                    await command.ToggleFavoriteAsync(content, TestContext.Current.CancellationToken);
                    reference[key] = current with { Favorite = !current.Favorite };
                    break;
            }
        }

        var expected = reference
            .Where(entry => entry.Value.Favorite || entry.Value.WatchLater || entry.Value.Rating is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var stored = (await repository.GetAllAsync(TestContext.Current.CancellationToken))
            .ToDictionary(state => state.Content.Value, state => state, StringComparer.Ordinal);

        Assert.Equal(expected.Count, stored.Count);
        foreach (var (key, value) in expected)
        {
            var actual = stored[key];
            Assert.Equal(value.Favorite, actual.IsFavorite);
            Assert.Equal(value.WatchLater, actual.IsWatchLater);
            Assert.Equal(value.Rating, actual.Rating);
        }
    }

    [Fact]
    public async Task An_unavailable_store_surfaces_its_failure_instead_of_pretending_to_save()
    {
        var command = new SetPersonalState(new BrokenPersonalStateRepository(), new FixedClock(Noon));

        await Assert.ThrowsAsync<IOException>(
            () => command.SetFavoriteAsync(Content(10), true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_use_cases_reject_missing_dependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new SetPersonalState(null!, new FixedClock(Noon)));
        Assert.Throws<ArgumentNullException>(
            () => new SetPersonalState(new MemoryPersonalStateRepository(), null!));
        Assert.Throws<ArgumentNullException>(() => new GetPersonalFilters(null!));
    }

    private sealed record ReferenceMarks(bool Favorite, bool WatchLater, int? Rating);

    private static ContentKey Content(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return ContentKey.ForTitle(new TitleId(new Guid(bytes)));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryPersonalStateRepository : IPersonalStateRepository
    {
        private readonly Dictionary<string, PersonalState> _states = new(StringComparer.Ordinal);

        public Task<PersonalState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_states.GetValueOrDefault(content.Value));
        }

        public Task<IReadOnlyList<PersonalState>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<PersonalState>>([.. _states.Values]);
        }

        public Task SaveAsync(
            PersonalState state,
            DateTimeOffset updatedUtc,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _states[state.Content.Value] = state;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(ContentKey content, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _states.Remove(content.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class BrokenPersonalStateRepository : IPersonalStateRepository
    {
        public Task<PersonalState?> GetAsync(ContentKey content, CancellationToken cancellationToken = default) =>
            Task.FromResult<PersonalState?>(null);

        public Task<IReadOnlyList<PersonalState>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new IOException("The personal-state store is unavailable.");

        public Task SaveAsync(
            PersonalState state,
            DateTimeOffset updatedUtc,
            CancellationToken cancellationToken = default) =>
            throw new IOException("The personal-state store is unavailable.");

        public Task DeleteAsync(ContentKey content, CancellationToken cancellationToken = default) =>
            throw new IOException("The personal-state store is unavailable.");
    }
}
