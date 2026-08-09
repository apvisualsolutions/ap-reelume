using System.Text.Json;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Privacy;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Privacy;

/// <summary>
/// What actually reaches the diagnostics file. Every canary below is something a person would not want
/// to hand over by accident, and the payload is searched for all of them, in the file that was written
/// rather than in the object that produced it.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DiagnosticsPayloadTests
{
    private const string PathCanary = "D:\\media\\canary-folder\\canary-file.mkv";
    private const string FileNameCanary = "canary-file.mkv";
    private const string TitleCanary = "Canary Title Nobody Should See";
    private const string TokenCanary = "canary-tmdb-token-3f9a2b";
    private const string ContentIdCanary = "canary-content-70f1";
    private const string HistoryCanary = "canary-watched-yesterday";
    private const string NasCredentialCanary = "canary-nas-user:canary-nas-password";
    private const string UserCanary = "canary-user-name";
    private const string MachineCanary = "CANARY-MACHINE";
    private const string SearchCanary = "canary-search-term";

    private static readonly string[] Canaries =
    [
        PathCanary,
        FileNameCanary,
        TitleCanary,
        TokenCanary,
        ContentIdCanary,
        HistoryCanary,
        NasCredentialCanary,
        UserCanary,
        MachineCanary,
        SearchCanary,
    ];

    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Without_consent_there_is_no_payload_at_all()
    {
        var report = new AllowlistedDiagnosticsBuilder().Build(
            new DiagnosticsConsent(IsGranted: false, GrantedUtc: null),
            SeededInputs());

        Assert.Null(report);
    }

    [Fact]
    public async Task Without_consent_nothing_is_written_to_disk_either()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));

        var written = await create.ExportAsync(
            new DiagnosticsConsent(IsGranted: false, GrantedUtc: null),
            SeededInputs(),
            TestContext.Current.CancellationToken);

        Assert.Null(written);
        Assert.False(Directory.Exists(paths.DiagnosticsDirectory));
    }

    [Fact]
    public async Task Not_one_canary_reaches_the_exported_file()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));

        var written = await create.ExportAsync(
            new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon),
            SeededInputs(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(written);
        var payload = await File.ReadAllTextAsync(written, TestContext.Current.CancellationToken);
        foreach (var canary in Canaries)
        {
            Assert.DoesNotContain(canary, payload, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("canary", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_preview_is_the_file_and_the_file_is_the_preview()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));
        var consent = new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon);

        var preview = create.Preview(consent, SeededInputs());
        var written = await create.ExportAsync(consent, SeededInputs(), TestContext.Current.CancellationToken);

        Assert.NotNull(preview);
        Assert.NotNull(written);
        Assert.Equal(
            DiagnosticsSerialization.Serialize(preview),
            await File.ReadAllTextAsync(written, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void The_payload_carries_only_what_the_allowlist_names()
    {
        var report = new AllowlistedDiagnosticsBuilder().Build(
            new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon),
            SeededInputs());

        Assert.NotNull(report);
        using var document = JsonDocument.Parse(DiagnosticsSerialization.Serialize(report));
        Assert.Equal(
            ["appVersion", "capabilities", "counts", "createdUtc", "errors", "formatVersion", "locale", "runtimeVersion", "windowsVersion"],
            document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Counts_travel_as_buckets_so_the_library_cannot_be_measured()
    {
        var report = new AllowlistedDiagnosticsBuilder().Build(
            new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon),
            SeededInputs());

        Assert.NotNull(report);
        var payload = DiagnosticsSerialization.Serialize(report);
        Assert.DoesNotContain("8423", payload, StringComparison.Ordinal);
        Assert.Equal("100+", Assert.Single(report.Counts, count => count.Name == "libraryItems").Bucket);
        Assert.All(report.Counts, count => Assert.Matches(@"^(0|1|2-5|6-20|21-100|100\+)$", count.Bucket));
        using var document = JsonDocument.Parse(payload);
        Assert.All(
            document.RootElement.GetProperty("counts").EnumerateArray(),
            count => Assert.Matches(
                @"^(0|1|2-5|6-20|21-100|100\+)$",
                count.GetProperty("bucket").GetString()!));
    }

    [Fact]
    public void An_exception_travels_as_its_type_and_never_as_its_message()
    {
        var report = new AllowlistedDiagnosticsBuilder().Build(
            new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon),
            SeededInputs());

        Assert.NotNull(report);
        var error = Assert.Single(report.Errors, entry => entry.Code == "scan-denied");
        Assert.Equal("System.UnauthorizedAccessException", error.Type);
        Assert.DoesNotContain(
            PathCanary,
            DiagnosticsSerialization.Serialize(report),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_serializer_refuses_a_type_the_contract_never_declared()
    {
        var entity = new LibraryRoot(
            new LibraryRootId(Guid.NewGuid()),
            PathCanary,
            RootKind.Local,
            RootAvailability.Available,
            ScanPolicy.Manual);

        var failure = Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Serialize(entity, DiagnosticsSerialization.Options));

        Assert.Contains("DiagnosticsJsonContext", failure.Message, StringComparison.Ordinal);
        Assert.Contains("metadata", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_export_replaces_the_previous_one_instead_of_piling_reports_up()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));
        var consent = new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon);

        await create.ExportAsync(consent, SeededInputs(), TestContext.Current.CancellationToken);
        var second = await create.ExportAsync(consent, SeededInputs(), TestContext.Current.CancellationToken);

        Assert.NotNull(second);
        Assert.Single(Directory.EnumerateFiles(paths.DiagnosticsDirectory));
    }

    /// <summary>
    /// Taking a permission back has to reach what the permission produced. A report that survives the
    /// consent that created it is a copy of somebody's machine sitting on their disk for no reason
    /// anybody agreed to.
    /// </summary>
    [Fact]
    public async Task Withdrawing_consent_deletes_the_report_that_was_already_written()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));
        var written = await create.ExportAsync(
            new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon),
            SeededInputs(),
            TestContext.Current.CancellationToken);
        Assert.NotNull(written);
        Assert.True(File.Exists(written));

        await create.DiscardAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(written));
        Assert.False(
            Directory.Exists(paths.DiagnosticsDirectory),
            "An empty diagnostics folder is still a trace of a report nobody consented to keep.");
    }

    [Fact]
    public async Task Discarding_a_report_that_was_never_written_is_not_an_error()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));

        await create.DiscardAsync(TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(paths.DiagnosticsDirectory));
    }

    [Fact]
    public void Turning_consent_off_again_produces_nothing_from_the_same_inputs()
    {
        var builder = new AllowlistedDiagnosticsBuilder();
        var inputs = SeededInputs();

        Assert.NotNull(builder.Build(new DiagnosticsConsent(true, Noon), inputs));
        Assert.Null(builder.Build(new DiagnosticsConsent(false, null), inputs));
    }

    [Fact]
    public async Task An_export_that_cannot_finish_leaves_no_half_written_file_behind()
    {
        using var directory = new DatabaseTestDirectory();
        var paths = new TestPaths(directory.Path);
        Directory.CreateDirectory(Path.Combine(paths.DiagnosticsDirectory, DiagnosticsReport.FileName));
        var create = new CreateDiagnostics(new AllowlistedDiagnosticsBuilder(), paths, new FixedClock(Noon));

        // Windows reports a file move onto a directory as access denied rather than as a plain I/O
        // failure, and the assertion names what actually happens instead of what would read better.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => create.ExportAsync(
            new DiagnosticsConsent(IsGranted: true, GrantedUtc: Noon),
            SeededInputs(),
            TestContext.Current.CancellationToken));

        Assert.Empty(Directory.EnumerateFiles(paths.DiagnosticsDirectory, "*.tmp"));
    }

    [Fact]
    public void Consent_survives_a_restart_and_an_undated_one_is_treated_as_no_consent()
    {
        using var directory = new DatabaseTestDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        var store = new JsonSettingsStore(settingsPath);

        Assert.False(new StoredPrivacySettings(store).Current.IsGranted);

        new StoredPrivacySettings(store).Save(new DiagnosticsConsent(true, Noon));
        var reloaded = new StoredPrivacySettings(new JsonSettingsStore(settingsPath)).Current;
        Assert.True(reloaded.IsGranted);
        Assert.Equal(Noon, reloaded.GrantedUtc);

        new StoredPrivacySettings(store).Save(new DiagnosticsConsent(false, null));
        Assert.False(new StoredPrivacySettings(store).Current.IsGranted);
        Assert.Null(new StoredPrivacySettings(store).Current.GrantedUtc);

        // A consent nobody can date is a consent nobody gave.
        new StoredPrivacySettings(store).Save(new DiagnosticsConsent(true, null));
        Assert.False(new StoredPrivacySettings(store).Current.IsGranted);
    }

    [Fact]
    public void A_settings_file_that_claims_consent_without_a_date_is_still_refused()
    {
        using var directory = new DatabaseTestDirectory();
        var settingsPath = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(settingsPath, "{\"privacy.diagnosticsEnabled\": true}");

        var settings = new StoredPrivacySettings(new JsonSettingsStore(settingsPath));

        Assert.False(settings.Current.IsGranted);
        Assert.Null(new AllowlistedDiagnosticsBuilder().Build(settings.Current, SeededInputs()));
    }

    /// <summary>
    /// Inputs with a canary in every category the specification prohibits, including a decoy NAS
    /// credential. The builder is handed all of it on purpose: refusing to carry it is the behaviour
    /// under test, not an accident of what the caller happened to pass.
    /// </summary>
    private static DiagnosticsInputs SeededInputs() => new(
        AppVersion: "1.0.0",
        WindowsVersion: "10.0.26200",
        RuntimeVersion: "10.0.0",
        Locale: "es-ES",
        HardwareAccelerationAvailable: true,
        HdrDisplayPresent: true,
        AudioEndpointCount: 4,
        LibraryItemCount: 8423,
        RootCount: 3,
        Errors:
        [
            new DiagnosticsErrorSample(
                "scan-denied",
                new UnauthorizedAccessException($"Access to '{PathCanary}' is denied for {UserCanary} on {MachineCanary}."),
                7),
            new DiagnosticsErrorSample(
                "identify-failed",
                new InvalidOperationException(
                    $"Could not identify {FileNameCanary} as {TitleCanary} ({ContentIdCanary}) searching {SearchCanary} with {TokenCanary}"),
                2),
            new DiagnosticsErrorSample("nas-auth", new IOException(NasCredentialCanary), 1),
            new DiagnosticsErrorSample("resume-missing", null, 1),
        ],
        History: [HistoryCanary],
        SearchTerms: [SearchCanary]);

    private sealed class TestPaths(string dataRoot) : IAppDataPaths
    {
        public string DataRoot { get; } = dataRoot;

        public string DatabasePath { get; } = Path.Combine(dataRoot, "library.db");

        public string SettingsPath { get; } = Path.Combine(dataRoot, "settings.json");

        public string BackupsDirectory { get; } = Path.Combine(dataRoot, "backups");

        public string PersonalArtworkDirectory { get; } = Path.Combine(dataRoot, "personal-artwork");

        public string RemoteCacheDirectory { get; } = Path.Combine(dataRoot, "cache", "artwork");

        public string DiagnosticsDirectory { get; } = Path.Combine(dataRoot, "diagnostics");
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
