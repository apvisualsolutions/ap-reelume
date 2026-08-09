using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using Xunit;

namespace ApSolutions.LocalMedia.PerformanceTests;

/// <summary>
/// Ten thousand candidates must be scored and ordered inside two hundred milliseconds once warm, which
/// is what keeps the rail from delaying Home.
/// </summary>
public sealed class RecommendationBudgetTests
{
    private const int CatalogSize = 10_000;
    private const double BudgetMilliseconds = 200;

    private static readonly string[] Genres =
        ["Drama", "Comedia", "Terror", "Ciencia ficción", "Documental", "Acción"];

    private static readonly string[] People = ["Ada", "Bruno", "Carmen", "Dídac", "Elena", "Fermín"];

    [Fact]
    public async Task Ten_thousand_candidates_are_ranked_inside_two_hundred_milliseconds()
    {
        var taste = BuildTaste();
        var candidates = BuildCandidates();

        var samples = UiFrameBudgetProbe.Measure(
            () =>
            {
                var ranked = RecommendationPolicy.Rank(taste, candidates);
                Assert.Equal(CatalogSize, ranked.Count);
            },
            repetitions: 10);

        await PerformanceEvidence.WriteAsync(
            "recommendation-rank-10k",
            samples,
            BudgetMilliseconds,
            TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < BudgetMilliseconds,
            $"Ranking ten thousand candidates had a p95 of {samples.P95Milliseconds:F3} ms.");
    }

    [Fact]
    public async Task The_whole_use_case_stays_inside_the_budget_on_a_ten_thousand_item_catalog()
    {
        var readModel = new InMemoryRecommendationReadModel(BuildTaste(), BuildCandidates());
        var useCase = new GetRecommendations(readModel);
        var options = new RecommendationOptions(IsEnabled: true);

        var samples = await PerformanceMeasurement.MeasureAsync(
            async () =>
            {
                var result = await useCase.ExecuteAsync(options, TestContext.Current.CancellationToken);
                Assert.Equal(CatalogSize, result.Count);
            },
            repetitions: 5);

        await PerformanceEvidence.WriteAsync(
            "recommendation-use-case-10k",
            samples,
            BudgetMilliseconds,
            TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < BudgetMilliseconds,
            $"The recommendation use case had a p95 of {samples.P95Milliseconds:F3} ms.");
    }

    [Fact]
    public async Task Switching_recommendations_off_costs_nothing_measurable()
    {
        var readModel = new InMemoryRecommendationReadModel(BuildTaste(), BuildCandidates());
        var useCase = new GetRecommendations(readModel);
        var options = new RecommendationOptions(IsEnabled: false);

        var samples = await PerformanceMeasurement.MeasureAsync(
            async () =>
            {
                var result = await useCase.ExecuteAsync(options, TestContext.Current.CancellationToken);
                Assert.Empty(result);
            },
            repetitions: 5);

        await PerformanceEvidence.WriteAsync(
            "recommendation-disabled",
            samples,
            budgetMilliseconds: 1,
            TestContext.Current.CancellationToken);
        Assert.True(
            samples.P95Milliseconds < 1,
            $"A disabled computation took {samples.P95Milliseconds:F3} ms.");
        Assert.Equal(0, readModel.Reads);
    }

    private static RecommendationTaste BuildTaste() => new(
        Genres
            .Select((genre, index) => (genre, affinity: (index % 3) - 1.0))
            .ToDictionary(entry => entry.genre, entry => entry.affinity, StringComparer.OrdinalIgnoreCase),
        People
            .Select((person, index) => (person, affinity: ((index % 3) - 1.0) / 2))
            .ToDictionary(entry => entry.person, entry => entry.affinity, StringComparer.OrdinalIgnoreCase),
        AverageRating: 7,
        PreferredYear: 2010);

    private static IReadOnlyList<RecommendationCandidate> BuildCandidates() =>
    [
        .. Enumerable.Range(0, CatalogSize).Select(index => new RecommendationCandidate(
            Title(index),
            [Genres[index % Genres.Length], Genres[(index + 2) % Genres.Length]],
            [People[index % People.Length]],
            1980 + (index % 46),
            IsAvailable: index % 10 != 0,
            IsWatched: index % 5 == 0,
            Rating: index % 7 == 0 ? (index % 10) + 1 : null))
    ];

    private static TitleId Title(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new TitleId(new Guid(bytes));
    }

    private sealed class InMemoryRecommendationReadModel(
        RecommendationTaste taste,
        IReadOnlyList<RecommendationCandidate> candidates) : IRecommendationReadModel
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Interlocked.Increment(ref _reads);
            return Task.FromResult(taste);
        }

        public Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = Interlocked.Increment(ref _reads);
            return Task.FromResult(candidates);
        }
    }
}
