// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Privacy;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Privacy;

/// <summary>
/// The claim this suite defends is narrow and checkable: the application makes no connection it was not
/// asked to make, and every HTTP client it can build has a declared purpose.
/// <para>
/// The observation happens inside the process, over the .NET event sources, exactly as ADR-0002 decided:
/// no proxy, no certificate, no elevation. It sees what this process does, not what the machine does.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
[Collection(ChildProcessSuites.Name)]
public sealed class NetworkPrivacyTests
{
    private const string PhaseVariable = "AP_LOCALMEDIA_NET_PHASE";
    private const string ReportVariable = "AP_LOCALMEDIA_NET_REPORT";

    private static readonly string[] ProfilerVariables =
    [
        "CORECLR_ENABLE_PROFILING",
        "CORECLR_PROFILER",
        "CORECLR_PROFILER_PATH",
        "CORECLR_PROFILER_PATH_32",
        "CORECLR_PROFILER_PATH_64",
        "COR_ENABLE_PROFILING",
        "COR_PROFILER",
        "COR_PROFILER_PATH",
    ];

    /// <summary>What one child process observed while doing one thing.</summary>
    private sealed record NetworkObservation(
        int Requests,
        int Resolutions,
        int CanaryRequests,
        int PayloadLength,
        string Sample);

    [Fact]
    public void Every_HTTP_client_the_source_tree_builds_has_a_declared_purpose()
    {
        // A field or a constructor parameter of that type is what actually makes a request. A comment
        // that merely says the words is not a network client, and treating it as one would train
        // everybody to add exceptions to this test.
        var declaration = new Regex(@"(?<![/*\w])HttpClient\s+(?!\w*\()[_a-zA-Z]", RegexOptions.CultureInvariant);
        var sourceRoot = Path.Combine(RepositoryLayout.Root, "src");
        var owners = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => declaration.IsMatch(StripComments(File.ReadAllText(file))))
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["ArtworkCache", "GitHubReleaseUpdateProvider", "TmdbMetadataProvider", "VerifiedUpdateDownloader"],
            owners);
        foreach (var owner in owners)
        {
            var purpose = NetworkPurposeRegistry.Find(owner);
            Assert.True(purpose is not null, $"No declared network purpose for {owner}.");
            Assert.False(string.IsNullOrWhiteSpace(purpose!.Reason));
            Assert.False(string.IsNullOrWhiteSpace(purpose.Host));
        }
    }

    [Fact]
    public void The_registry_names_only_hosts_the_product_actually_asks_for()
    {
        Assert.Equal(
            ["api.github.com", "api.themoviedb.org", "github.com", "image.tmdb.org"],
            NetworkPurposeRegistry.Declared.Select(purpose => purpose.Host).Order(StringComparer.Ordinal));
        Assert.All(NetworkPurposeRegistry.Declared, purpose => Assert.True(purpose.RequiresConsent));
        Assert.True(NetworkPurposeRegistry.IsDeclaredHost("api.themoviedb.org"));
        Assert.True(NetworkPurposeRegistry.IsDeclaredHost("image.tmdb.org"));
        Assert.False(NetworkPurposeRegistry.IsDeclaredHost("telemetry.example.com"));
        Assert.False(NetworkPurposeRegistry.IsDeclaredHost("   "));
        Assert.Null(NetworkPurposeRegistry.Find("SomethingUndeclared"));
        Assert.Null(NetworkPurposeRegistry.Find("  "));
        Assert.Equal("image.tmdb.org", NetworkPurposeRegistry.Require("ArtworkCache").Host);
        var undeclared = Assert.Throws<InvalidOperationException>(
            () => NetworkPurposeRegistry.Require("SomethingUndeclared"));
        Assert.Contains("SomethingUndeclared", undeclared.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_in_the_registry_carries_anything_but_a_purpose()
    {
        foreach (var purpose in NetworkPurposeRegistry.Declared)
        {
            Assert.DoesNotContain("token", purpose.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", purpose.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\\", purpose.Reason, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_application_owns_no_credential_store_of_its_own()
    {
        var sourceRoot = Path.Combine(RepositoryLayout.Root, "src");
        var offenders = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var source = File.ReadAllText(file);
                return source.Contains("CredentialCache", StringComparison.Ordinal)
                    || source.Contains("NetworkCredential", StringComparison.Ordinal)
                    || source.Contains("CredWrite", StringComparison.Ordinal)
                    || source.Contains("ProtectedData", StringComparison.Ordinal);
            })
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task Building_and_exporting_a_diagnostic_report_opens_no_connection_at_all()
    {
        var observed = await MeasureInChildAsync("quiet");

        Assert.True(observed.Requests == 0, $"Unexpected HTTP request: {observed.Sample}");
        Assert.True(observed.Resolutions == 0, $"Unexpected name resolution: {observed.Sample}");
        Assert.Equal(0, observed.CanaryRequests);
        Assert.True(observed.PayloadLength > 0, "The child produced no report to measure.");
    }

    /// <summary>
    /// LIB-016's acceptance, measured rather than read: with the automatic refresh off, a pass opens
    /// nothing at all. The same child then turns it on and the canary counts one request per stale
    /// entry, so the zero above is a zero and not a blind spot.
    /// </summary>
    [Fact]
    public async Task The_automatic_refresh_switched_off_opens_no_connection()
    {
        var observed = await MeasureInChildAsync("refresh");

        Assert.Equal(0, observed.PayloadLength);
        Assert.Equal(2, observed.CanaryRequests);
        Assert.True(observed.Requests >= 2, $"The listener saw no request although two were made: {observed.Sample}");
    }

    /// <summary>
    /// Proves the harness is not simply blind. A measurement of zero means nothing until the same
    /// measurement has been shown to reach one.
    /// </summary>
    [Fact]
    public async Task The_same_observation_does_see_a_request_when_one_is_actually_made()
    {
        var observed = await MeasureInChildAsync("noisy");

        Assert.Equal(1, observed.CanaryRequests);
        Assert.True(observed.Requests >= 1, "The listener saw no request although one was made.");
    }

    /// <summary>
    /// One phase inside this process. Only the child started above enters here, because an event
    /// listener sees the whole process and the shared test host is busy doing other things.
    /// </summary>
    [Fact]
    public async Task Network_probe_child_fixture()
    {
        var phase = Environment.GetEnvironmentVariable(PhaseVariable);
        var reportPath = Environment.GetEnvironmentVariable(ReportVariable);
        if (string.IsNullOrWhiteSpace(phase) || string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        using var listener = new NetworkActivityListener();
        using var canary = new CanaryServer();
        var payloadLength = 0;

        if (string.Equals(phase, "quiet", StringComparison.Ordinal))
        {
            var report = new AllowlistedDiagnosticsBuilder().Build(
                new DiagnosticsConsent(IsGranted: true, GrantedUtc: DateTimeOffset.UnixEpoch),
                MinimalInputs());
            payloadLength = DiagnosticsSerialization.Serialize(report!).Length;
        }
        else if (string.Equals(phase, "refresh", StringComparison.Ordinal))
        {
            // LIB-016, both halves in one child: the pass with the switch off, whose count is
            // reported as the payload length, and then the same pass with it on, so the zero above
            // is a measured zero rather than a blind one.
            var settings = new SwitchableAutoRefresh(enabled: false);
            var repository = new TwoStaleEntries();
            var pass = new RefreshStaleMetadata(
                repository,
                new RefreshMetadata(
                    repository,
                    new CanaryMetadataProvider(canary.Address),
                    new MetadataMergePolicy(),
                    new MetadataLanguage("es-ES", "en-US"),
                    TimeProvider.System),
                settings,
                new IdlePlayback(),
                new IdleScans(),
                TimeProvider.System);

            _ = await pass.ExecuteAsync(TestContext.Current.CancellationToken);
            payloadLength = canary.RequestCount;
            settings.SetAutomaticRefreshEnabled(true);
            _ = await pass.ExecuteAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(canary.Address, TestContext.Current.CancellationToken);
            payloadLength = response.IsSuccessStatusCode ? 1 : 0;
        }

        await Task.Delay(400, TestContext.Current.CancellationToken);
        var observed = string.Join(
            " | ",
            listener.Requests.Concat(listener.Resolutions).Take(3));
        await File.WriteAllTextAsync(
            reportPath,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{listener.Requests.Count};{listener.Resolutions.Count};{canary.RequestCount};{payloadLength};{observed}"),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A host in the source is either connected to, handed to another application, or neither.
    /// </summary>
    /// <remarks>
    /// The third list is the boring one: XML namespaces and licence addresses that nothing ever
    /// opens. The second arrived with the provider trailer (LIB-015), and it is separate from the
    /// first on purpose — declaring <c>www.youtube.com</c> as a network purpose would claim a
    /// connection this application never makes and would widen the check the network canary trusts,
    /// while leaving it out of the registry entirely would put a real privacy decision in a list of
    /// markup details.
    /// </remarks>
    [Fact]
    public void No_source_file_names_a_host_that_is_neither_declared_nor_handed_off()
    {
        var sourceRoot = Path.Combine(RepositoryLayout.Root, "src");
        var hostPattern = new Regex(@"https?://(?<host>[a-z0-9.\-]+)", RegexOptions.IgnoreCase);
        var openedByNobody = new[] { "github.com", "schemas.microsoft.com", "learn.microsoft.com", "www.gnu.org", "avaloniaui.net" };

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            foreach (Match match in hostPattern.Matches(File.ReadAllText(file)))
            {
                var host = match.Groups["host"].Value;
                Assert.True(
                    NetworkPurposeRegistry.IsDeclaredHost(host)
                        || NetworkPurposeRegistry.IsHandedOffHost(host)
                        || openedByNobody.Contains(host, StringComparer.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)} names an undeclared host: {host}");
            }
        }
    }

    /// <summary>
    /// The two lists must stay apart. A destination that is only handed to a browser must never
    /// answer yes to "is this a host this application may connect to?" — that question is what the
    /// canary and the client construction rely on.
    /// </summary>
    [Fact]
    public void A_handed_off_destination_is_not_a_host_this_application_may_connect_to()
    {
        Assert.NotEmpty(NetworkPurposeRegistry.HandedOff);
        foreach (var destination in NetworkPurposeRegistry.HandedOff)
        {
            Assert.False(NetworkPurposeRegistry.IsDeclaredHost(destination.Host));
            Assert.True(NetworkPurposeRegistry.IsHandedOffHost(destination.Host));
            Assert.NotEmpty(destination.Purpose);
        }
    }

    /// <summary>Runs one phase in a child process and reads back what it observed.</summary>
    private static async Task<NetworkObservation> MeasureInChildAsync(string phase)
    {
        var reportPath = Path.Combine(
            Path.GetTempPath(),
            "APSolutions.LocalMedia.Tests",
            $"network-{phase}-{Guid.NewGuid():N}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(Path.Combine(
            RepositoryLayout.Root,
            "tests",
            "ApSolutions.LocalMedia.IntegrationTests",
            "ApSolutions.LocalMedia.IntegrationTests.csproj"));
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(configuration);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add("FullyQualifiedName~Network_probe_child_fixture");
        startInfo.Environment[PhaseVariable] = phase;
        startInfo.Environment[ReportVariable] = reportPath;
        foreach (var profiling in ProfilerVariables)
        {
            startInfo.Environment[profiling] = string.Empty;
        }

        using var child = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the network probe.");
        var output = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await child.WaitForExitAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(output, error);

        if (!File.Exists(reportPath))
        {
            Assert.Fail($"The network probe wrote nothing. stdout={await output}; stderr={await error}");
        }

        var values = (await File.ReadAllTextAsync(reportPath, TestContext.Current.CancellationToken))
            .Split(';', 5);
        File.Delete(reportPath);
        return new NetworkObservation(
            int.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(values[2], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(values[3], System.Globalization.CultureInfo.InvariantCulture),
            values.Length > 4 ? values[4] : string.Empty);
    }

    /// <summary>Two identified entries, both older than the stale window, and nothing else.</summary>
    private sealed class TwoStaleEntries : ICatalogMetadataRepository
    {
        private readonly Dictionary<TitleId, CatalogMetadata> _rows = new[] { "movie/1", "movie/2" }
            .Select(key => new CatalogMetadata(
                new TitleId(Guid.NewGuid()),
                new EditableMetadata(
                    "Stored",
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
                key,
                DateTimeOffset.UtcNow.AddDays(-200)))
            .ToDictionary(entry => entry.TitleId);

        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_rows.TryGetValue(titleId, out var found) ? found : null);

        public Task<MetadataWriteResult> TrySaveAsync(
            CatalogMetadata catalog,
            int expectedRevision,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MetadataWriteResult(
                MetadataWriteOutcome.Applied,
                catalog is null ? null : catalog with { Revision = expectedRevision + 1 }));

        public Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
            DateTimeOffset staleBefore,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogMetadata>>([.. _rows.Values.Take(limit)]);
    }

    /// <summary>
    /// A provider that reaches the canary when it is asked about a title. What it answers does not
    /// matter here; that it was asked at all is the whole measurement.
    /// </summary>
    private sealed class CanaryMetadataProvider(Uri address) : IMetadataProvider
    {
        private static readonly HttpClient Client = new();

        public string Name => "tmdb";

        public MetadataReference? TryCreateReference(string key) =>
            new(Name, key, MetadataContentKind.Movie);

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataSearchResult>>([]);

        public async Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default)
        {
            using var response = await Client.GetAsync(address, cancellationToken);
            return response.IsSuccessStatusCode
                ? new MetadataDetails(
                    reference,
                    language.Primary,
                    "Refreshed",
                    OriginalTitle: null,
                    Overview: null,
                    ReleaseYear: null,
                    Genres: [],
                    PosterPath: null,
                    BackdropPath: null,
                    TrailerKey: null)
                : null;
        }
    }

    private sealed class SwitchableAutoRefresh(bool enabled) : IAutoRefreshSettings
    {
        public bool AutomaticRefreshEnabled { get; private set; } = enabled;

        public void SetAutomaticRefreshEnabled(bool value) => AutomaticRefreshEnabled = value;
    }

    private sealed class IdlePlayback : IPlaybackActivity
    {
        public bool IsPlaybackActive => false;
    }

    private sealed class IdleScans : IScanActivity
    {
        public bool IsScanActive => false;
    }

    /// <summary>Removes comments so a sentence about HTTP is not mistaken for an HTTP client.</summary>
    private static string StripComments(string source) =>
        Regex.Replace(source, @"//.*?$|/\*.*?\*/", string.Empty, RegexOptions.Multiline | RegexOptions.Singleline);

    private static DiagnosticsInputs MinimalInputs() => new(
        AppVersion: "1.0.0",
        WindowsVersion: "10.0.26200",
        RuntimeVersion: "10.0.0",
        Locale: "es-ES",
        HardwareAccelerationAvailable: true,
        HdrDisplayPresent: false,
        AudioEndpointCount: 2,
        LibraryItemCount: 12,
        RootCount: 1,
        Errors: [],
        History: [],
        SearchTerms: []);

    /// <summary>
    /// Records every request, connection, and name resolution this process performs, from the .NET event
    /// sources themselves. It sees the URI before anything is encrypted, which is the whole point.
    /// </summary>
    private sealed class NetworkActivityListener : EventListener
    {
        private readonly List<string> _requests = [];
        private readonly List<string> _resolutions = [];
        private readonly Lock _gate = new();

        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public IReadOnlyList<string> Resolutions
        {
            get
            {
                lock (_gate)
                {
                    return [.. _resolutions];
                }
            }
        }

        private static bool IsLoopback(string payload) =>
            payload.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || payload.Contains("127.0.0.1", StringComparison.Ordinal)
            || payload.Contains("::1", StringComparison.Ordinal)
            || payload.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name is "System.Net.Http" or "System.Net.Sockets" or "System.Net.NameResolution")
            {
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var payload = string.Join(
                ' ',
                eventData.Payload?.Select(value => value?.ToString() ?? string.Empty) ?? []);
            lock (_gate)
            {
                if (eventData.EventSource.Name == "System.Net.Http" && eventData.EventName == "RequestStart")
                {
                    _requests.Add(payload);
                }
                else if (eventData.EventSource.Name == "System.Net.NameResolution"
                    && eventData.EventName == "ResolutionStart"
                    && !IsLoopback($"{eventData.EventName} {payload}"))
                {
                    // The test host itself resolves loopback to talk to its own runner. That is the
                    // harness, not the application, and counting it would make the measurement about
                    // the wrong process.
                    _resolutions.Add($"{eventData.EventName}({eventData.Payload?.Count ?? 0}) [{payload}]");
                }
            }
        }
    }

    /// <summary>
    /// A local server that answers the least possible and counts everything, as in T32. It is a raw
    /// TCP listener rather than an <see cref="HttpListener"/> on purpose: the latter resolves a name
    /// while binding, and that resolution would show up in the very measurement being taken.
    /// </summary>
    private sealed class CanaryServer : IDisposable
    {
        private static readonly byte[] EmptyResponse =
            "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray();

        private readonly TcpListener _listener;
        private int _requestCount;

        public CanaryServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Address = new Uri($"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/");
            _ = Task.Run(AcceptAsync);
        }

        public Uri Address { get; }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Dispose() => _listener.Dispose();

        private async Task AcceptAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
                {
                    return;
                }

                using (client)
                {
                    Interlocked.Increment(ref _requestCount);
                    var stream = client.GetStream();
                    var buffer = new byte[1024];
                    _ = await stream.ReadAsync(buffer).ConfigureAwait(false);
                    await stream.WriteAsync(EmptyResponse).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
