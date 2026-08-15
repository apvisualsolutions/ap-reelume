// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Tests.Identification;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Metadata;

/// <summary>
/// LIB-016. The switch is off until somebody turns it on, and off has to mean nothing happens at
/// all — not a request that gets discarded afterwards.
/// </summary>
public sealed class RefreshStaleMetadataTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task With_the_switch_off_the_catalogue_is_not_even_read()
    {
        var world = new World(enabled: false, StaleEntry("movie/1"));

        var result = await world.Refresh.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RefreshStaleMetadataResult.None, result);
        Assert.Equal(0, world.Repository.StaleQueries);
        Assert.Empty(world.Provider.Requested);
    }

    [Fact]
    public async Task With_the_switch_on_it_asks_the_provider_about_what_the_query_returned()
    {
        var world = new World(enabled: true, StaleEntry("movie/1"), StaleEntry("movie/2"));

        var result = await world.Refresh.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new RefreshStaleMetadataResult(2, 2), result);
        Assert.Equal(
            ["movie/1", "movie/2"],
            world.Provider.Requested.Select(reference => reference.Key).ToArray());
    }

    /// <summary>
    /// The cap and the window are the policy's, not this class's: what is pinned here is that the
    /// policy's numbers are the ones the query is asked with.
    /// </summary>
    [Fact]
    public async Task The_pass_asks_for_the_policy_window_and_the_policy_cap()
    {
        var world = new World(enabled: true, StaleEntry("movie/1"));

        _ = await world.Refresh.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MetadataRefreshPolicy.MaximumPerPass, world.Repository.LastLimit);
        Assert.Equal(MetadataRefreshPolicy.StaleBefore(Now), world.Repository.LastStaleBefore);
    }

    [Fact]
    public async Task An_open_video_comes_first()
    {
        var world = new World(enabled: true, StaleEntry("movie/1")) { Playback = { IsPlaybackActive = true } };

        Assert.Equal(RefreshStaleMetadataResult.None, await world.Refresh.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, world.Repository.StaleQueries);
    }

    [Fact]
    public async Task A_scan_that_is_running_comes_first()
    {
        var world = new World(enabled: true, StaleEntry("movie/1")) { Scans = { IsScanActive = true } };

        Assert.Equal(RefreshStaleMetadataResult.None, await world.Refresh.ExecuteAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, world.Repository.StaleQueries);
    }

    /// <summary>
    /// A pass outlives the moment it started in, so the answer to "is somebody watching?" is asked
    /// again before each entry rather than once at the top.
    /// </summary>
    [Fact]
    public async Task Playback_that_starts_mid_pass_stops_the_rest_of_it()
    {
        var world = new World(enabled: true, StaleEntry("movie/1"), StaleEntry("movie/2"), StaleEntry("movie/3"));
        world.Provider.OnRequested = () => world.Playback.IsPlaybackActive = true;

        var result = await world.Refresh.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Attempted);
        Assert.Single(world.Provider.Requested);
    }

    private static CatalogMetadata StaleEntry(string providerKey) => new(
        new TitleId(Guid.NewGuid()),
        new EditableMetadata(
            "Stored title",
            OriginalTitle: null,
            Overview: null,
            ReleaseYear: null,
            Genres: [],
            PosterPath: null,
            BackdropPath: null,
            TrailerKey: null,
            LockedFields: new HashSet<MetadataField>()),
        Revision: 1,
        "tmdb",
        providerKey,
        Now.AddDays(-200));

    /// <summary>
    /// Built the way the composition builds it: the real <see cref="RefreshMetadata"/> over a real
    /// merge policy, so what is measured is the pass, not a stand-in for it.
    /// </summary>
    private sealed class World
    {
        public World(bool enabled, params CatalogMetadata[] stale)
        {
            Provider = new RecordingProvider(stale
                .Select(entry => new MetadataDetails(
                    new MetadataReference("tmdb", entry.ProviderKey!, MetadataContentKind.Movie),
                    "es-ES",
                    "Refreshed title",
                    OriginalTitle: null,
                    Overview: "Refreshed overview",
                    ReleaseYear: 2001,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: null))
                .ToArray());
            Repository = new RecordingRepository(stale);
            Refresh = new RefreshStaleMetadata(
                Repository,
                new RefreshMetadata(
                    Repository,
                    Provider,
                    new MetadataMergePolicy(),
                    new MetadataLanguage("es-ES", "en-US"),
                    new FixedTime()),
                new Settings(enabled),
                Playback,
                Scans,
                new FixedTime());
        }

        public RecordingProvider Provider { get; }

        public RecordingRepository Repository { get; }

        public FakePlayback Playback { get; } = new();

        public FakeScans Scans { get; } = new();

        public RefreshStaleMetadata Refresh { get; }
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakePlayback : IPlaybackActivity
    {
        public bool IsPlaybackActive { get; set; }
    }

    private sealed class FakeScans : IScanActivity
    {
        public bool IsScanActive { get; set; }
    }

    private sealed class Settings(bool enabled) : IAutoRefreshSettings
    {
        public bool AutomaticRefreshEnabled { get; private set; } = enabled;

        public void SetAutomaticRefreshEnabled(bool value) => AutomaticRefreshEnabled = value;
    }

    private sealed class RecordingProvider(params MetadataDetails[] details) : IMetadataProvider
    {
        private readonly StubMetadataProvider _inner = new(details);

        public string Name => _inner.Name;

        public List<MetadataReference> Requested { get; } = [];

        public Action? OnRequested { get; set; }

        public MetadataReference? TryCreateReference(string key) => _inner.TryCreateReference(key);

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            _inner.SearchAsync(query, language, cancellationToken);

        public Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default)
        {
            Requested.Add(reference);
            OnRequested?.Invoke();
            return _inner.GetDetailsAsync(reference, language, cancellationToken);
        }
    }

    /// <summary>
    /// Returns what it was given rather than deciding staleness itself — that decision belongs to a
    /// SQL statement and is measured against it.
    /// </summary>
    private sealed class RecordingRepository(IReadOnlyList<CatalogMetadata> stale) : ICatalogMetadataRepository
    {
        private readonly Dictionary<TitleId, CatalogMetadata> _rows =
            stale.ToDictionary(entry => entry.TitleId);

        public int StaleQueries { get; private set; }

        public int LastLimit { get; private set; }

        public DateTimeOffset LastStaleBefore { get; private set; }

        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rows.TryGetValue(titleId, out var found) ? found : null);

        public Task<MetadataWriteResult> TrySaveAsync(
            CatalogMetadata catalog,
            int expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            if ((_rows.TryGetValue(catalog.TitleId, out var stored) ? stored.Revision : 0) != expectedRevision)
            {
                return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Conflict, stored));
            }

            var written = catalog with { Revision = expectedRevision + 1 };
            _rows[catalog.TitleId] = written;
            return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Applied, written));
        }

        public Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
            DateTimeOffset staleBefore,
            int limit,
            CancellationToken cancellationToken = default)
        {
            StaleQueries++;
            LastLimit = limit;
            LastStaleBefore = staleBefore;
            return Task.FromResult<IReadOnlyList<CatalogMetadata>>([.. stale]);
        }
    }
}
