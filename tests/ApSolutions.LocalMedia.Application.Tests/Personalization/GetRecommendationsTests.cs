// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Reflection;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Personalization;

/// <summary>
/// Recommendations are computed locally, explained in reason codes, and can be switched off. When they
/// are off the use case does no work at all, and in no case does anything leave the machine.
/// </summary>
public sealed class GetRecommendationsTests
{
    [Fact]
    public async Task Disabled_returns_nothing_and_reads_nothing_at_all()
    {
        var readModel = new CountingReadModel();
        var useCase = new GetRecommendations(readModel);

        var result = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: false),
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
        Assert.Equal(0, readModel.TasteReads);
        Assert.Equal(0, readModel.CandidateReads);
    }

    [Fact]
    public async Task Enabled_reads_once_and_returns_ranked_explained_results()
    {
        var readModel = new CountingReadModel
        {
            Taste = new RecommendationTaste(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 1.0 },
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                AverageRating: 8,
                PreferredYear: 2016),
            Candidates =
            [
                Candidate(1, ["Comedia"], isAvailable: true, isWatched: false),
                Candidate(2, ["Drama"], isAvailable: true, isWatched: false),
            ],
        };

        var result = await new GetRecommendations(readModel).ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.Equal(Title(2), result[0].ContentId);
        Assert.Contains(RecommendationReason.GenreMatch, result[0].ReasonCodes);
        Assert.Equal(1, readModel.TasteReads);
        Assert.Equal(1, readModel.CandidateReads);
    }

    [Fact]
    public async Task Excluding_unavailable_content_is_configurable_and_off_by_default()
    {
        var readModel = new CountingReadModel
        {
            Candidates =
            [
                Candidate(1, ["Drama"], isAvailable: false, isWatched: false),
                Candidate(2, ["Drama"], isAvailable: true, isWatched: false),
            ],
        };
        var useCase = new GetRecommendations(readModel);

        var included = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);
        Assert.Equal(2, included.Count);

        var excluded = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: true, ExcludeUnavailable: true),
            TestContext.Current.CancellationToken);
        var single = Assert.Single(excluded);
        Assert.Equal(Title(2), single.ContentId);
    }

    [Fact]
    public async Task The_limit_trims_the_result_without_changing_the_order()
    {
        var readModel = new CountingReadModel
        {
            Candidates = [.. Enumerable.Range(1, 30)
                .Select(seed => Candidate(seed, ["Drama"], isAvailable: true, isWatched: false))],
        };

        var full = await new GetRecommendations(readModel).ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);
        var trimmed = await new GetRecommendations(readModel).ExecuteAsync(
            new RecommendationOptions(IsEnabled: true, Limit: 5),
            TestContext.Current.CancellationToken);

        Assert.Equal(30, full.Count);
        Assert.Equal(5, trimmed.Count);
        Assert.Equal(
            full.Take(5).Select(item => item.ContentId),
            trimmed.Select(item => item.ContentId));
    }

    [Fact]
    public async Task The_same_input_produces_the_same_output_on_a_second_run()
    {
        var readModel = new CountingReadModel
        {
            Taste = new RecommendationTaste(
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Drama"] = 0.7 },
                new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["Ada"] = 0.3 },
                AverageRating: 7,
                PreferredYear: 2010),
            Candidates = [.. Enumerable.Range(1, 100).Select(seed => Candidate(
                seed,
                seed % 2 == 0 ? ["Drama"] : ["Comedia"],
                isAvailable: true,
                isWatched: seed % 3 == 0))],
        };
        var useCase = new GetRecommendations(readModel);

        var first = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);
        var second = await useCase.ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            first.Select(item => (item.ContentId, item.Score)),
            second.Select(item => (item.ContentId, item.Score)));
    }

    [Fact]
    public async Task An_empty_catalog_returns_nothing_without_failing()
    {
        var result = await new GetRecommendations(new CountingReadModel()).ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Nothing_reaches_a_canary_server_while_recommendations_are_computed()
    {
        using var canary = new CanaryServer();
        var readModel = new CountingReadModel
        {
            Candidates = [.. Enumerable.Range(1, 200)
                .Select(seed => Candidate(seed, ["Drama"], isAvailable: true, isWatched: false))],
        };

        var result = await new GetRecommendations(readModel).ExecuteAsync(
            new RecommendationOptions(IsEnabled: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(200, result.Count);
        Assert.Equal(0, canary.RequestCount);
    }

    [Fact]
    public void Neither_the_application_nor_the_domain_assembly_references_an_http_stack()
    {
        foreach (var name in new[] { "ApSolutions.LocalMedia.Application", "ApSolutions.LocalMedia.Domain" })
        {
            var references = Assembly.Load(name).GetReferencedAssemblies();
            Assert.DoesNotContain(
                references,
                reference => reference.Name?.Contains("Http", StringComparison.OrdinalIgnoreCase) is true
                    || reference.Name?.Equals("System.Net.Sockets", StringComparison.OrdinalIgnoreCase) is true
                    || reference.Name?.Equals("System.Net.Requests", StringComparison.OrdinalIgnoreCase) is true);
        }
    }

    [Fact]
    public async Task The_use_case_rejects_a_missing_read_model_and_a_missing_options_object()
    {
        Assert.Throws<ArgumentNullException>(() => new GetRecommendations(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => new GetRecommendations(new CountingReadModel()).ExecuteAsync(
                null!,
                TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecommendationOptions(true, false, 0));
    }

    private static RecommendationCandidate Candidate(
        int seed,
        string[] genres,
        bool isAvailable,
        bool isWatched) => new(
        Title(seed),
        genres,
        [],
        2016,
        isAvailable,
        isWatched,
        Rating: null);

    private static TitleId Title(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new TitleId(new Guid(bytes));
    }

    private sealed class CountingReadModel : IRecommendationReadModel
    {
        public int TasteReads { get; private set; }

        public int CandidateReads { get; private set; }

        public RecommendationTaste Taste { get; init; } = RecommendationTaste.Empty;

        public IReadOnlyList<RecommendationCandidate> Candidates { get; init; } = [];

        public Task<RecommendationTaste> ReadTasteAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TasteReads++;
            return Task.FromResult(Taste);
        }

        public Task<IReadOnlyList<RecommendationCandidate>> ReadCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CandidateReads++;
            return Task.FromResult(Candidates);
        }
    }

    /// <summary>
    /// A loopback listener that answers nothing. If any code under test tried to reach the network by
    /// way of a proxy or a hard-coded endpoint, its request count would stop being zero.
    /// </summary>
    private sealed class CanaryServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private int _requestCount;

        public CanaryServer()
        {
            for (var port = 51_000; port < 51_050; port++)
            {
                try
                {
                    _listener.Prefixes.Clear();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException)
                {
                    // The port was taken; try the next one.
                }
            }

            if (_listener.IsListening)
            {
                _ = AcceptAsync();
            }
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Dispose()
        {
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            _listener.Close();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync().ConfigureAwait(false);
                    _ = Interlocked.Increment(ref _requestCount);
                    context.Response.StatusCode = 204;
                    context.Response.Close();
                }
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                // The listener was stopped by Dispose.
            }
        }
    }
}
