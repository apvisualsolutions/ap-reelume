// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Infrastructure.Privacy;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The consent surface. It starts off, it says what would be sent before anything is, and turning it
/// back off is never harder than turning it on.
/// </summary>
public sealed class PrivacyConsentTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Diagnostics_start_switched_off()
    {
        var settings = new InMemoryPrivacySettings();
        var viewModel = CreateViewModel(settings);

        Assert.False(viewModel.DiagnosticsEnabled);
        Assert.False(settings.Current.IsGranted);
        Assert.Null(viewModel.PreviewJson);
        Assert.False(viewModel.HasPreview);
        Assert.Equal("PrivacyStatusOff", viewModel.StatusKey);
    }

    [Fact]
    public void Turning_diagnostics_on_records_when_consent_was_given()
    {
        var settings = new InMemoryPrivacySettings();
        var viewModel = CreateViewModel(settings);

        viewModel.DiagnosticsEnabled = true;

        Assert.True(settings.Current.IsGranted);
        Assert.Equal(Noon, settings.Current.GrantedUtc);
        Assert.Equal("PrivacyStatusOn", viewModel.StatusKey);
    }

    [Fact]
    public void Turning_them_off_again_clears_the_consent_and_the_preview()
    {
        var settings = new InMemoryPrivacySettings();
        var viewModel = CreateViewModel(settings);
        viewModel.DiagnosticsEnabled = true;
        viewModel.PreviewCommand.Execute(null);
        Assert.True(viewModel.HasPreview);

        viewModel.DiagnosticsEnabled = false;

        Assert.False(settings.Current.IsGranted);
        Assert.Null(settings.Current.GrantedUtc);
        Assert.Null(viewModel.PreviewJson);
        Assert.False(viewModel.HasPreview);
        Assert.Equal("PrivacyStatusOff", viewModel.StatusKey);
    }

    /// <summary>
    /// Withdrawing the consent has to reach the file the consent produced. Clearing the preview while
    /// the exported report stays on disk is the appearance of taking a permission back, not the fact.
    /// </summary>
    [Fact]
    public async Task Turning_them_off_discards_the_report_that_was_already_exported()
    {
        var discards = 0;
        var settings = new InMemoryPrivacySettings();
        var viewModel = new PrivacySettingsViewModel(
            settings,
            new AllowlistedDiagnosticsBuilder(),
            () => Inputs(),
            (_, _, _) => Task.FromResult<string?>("D:\\data\\diagnostics\\report.json"),
            () => Noon,
            NetworkPurposeRegistry.Declared,
            _ =>
            {
                discards++;
                return Task.CompletedTask;
            });
        viewModel.DiagnosticsEnabled = true;
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        Assert.Equal("report.json", viewModel.ExportedFileName);

        viewModel.DiagnosticsEnabled = false;

        Assert.Equal(1, discards);
        Assert.Null(viewModel.ExportedFileName);
    }

    [Fact]
    public void Turning_them_on_discards_nothing()
    {
        var discards = 0;
        var viewModel = new PrivacySettingsViewModel(
            new InMemoryPrivacySettings(),
            new AllowlistedDiagnosticsBuilder(),
            () => Inputs(),
            (_, _, _) => Task.FromResult<string?>(null),
            () => Noon,
            NetworkPurposeRegistry.Declared,
            _ =>
            {
                discards++;
                return Task.CompletedTask;
            });

        viewModel.DiagnosticsEnabled = true;

        Assert.Equal(0, discards);
    }

    [Fact]
    public void Asking_for_a_preview_while_diagnostics_are_off_shows_nothing()
    {
        var viewModel = CreateViewModel(new InMemoryPrivacySettings());

        viewModel.PreviewCommand.Execute(null);

        Assert.Null(viewModel.PreviewJson);
        Assert.False(viewModel.HasPreview);
        Assert.False(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public void The_preview_shows_the_exact_text_an_export_would_write()
    {
        var viewModel = CreateViewModel(new InMemoryPrivacySettings());
        viewModel.DiagnosticsEnabled = true;

        viewModel.PreviewCommand.Execute(null);

        Assert.True(viewModel.HasPreview);
        Assert.NotNull(viewModel.PreviewJson);
        Assert.Contains("\"formatVersion\"", viewModel.PreviewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("D:\\", viewModel.PreviewJson, StringComparison.Ordinal);
        Assert.DoesNotContain("token", viewModel.PreviewJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exporting_names_the_file_and_never_the_folder_it_went_to()
    {
        var viewModel = CreateViewModel(
            new InMemoryPrivacySettings(),
            export: (_, _, _) => Task.FromResult<string?>("D:\\personal folder\\diagnostics\\report.json"));
        viewModel.DiagnosticsEnabled = true;

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal("report.json", viewModel.ExportedFileName);
        Assert.DoesNotContain("personal", viewModel.ExportedFileName!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("PrivacyStatusExported", viewModel.StatusKey);
    }

    [Fact]
    public async Task Exporting_with_consent_withdrawn_writes_nothing_and_says_nothing_was_written()
    {
        var attempts = 0;
        var viewModel = CreateViewModel(
            new InMemoryPrivacySettings(),
            export: (_, _, _) =>
            {
                attempts++;
                return Task.FromResult<string?>(null);
            });

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, attempts);
        Assert.Null(viewModel.ExportedFileName);
        Assert.Equal("PrivacyStatusOff", viewModel.StatusKey);
    }

    [Fact]
    public async Task The_export_command_does_what_the_method_does()
    {
        var exports = 0;
        using var finished = new SemaphoreSlim(0, 1);
        var viewModel = CreateViewModel(
            new InMemoryPrivacySettings(),
            export: (_, _, _) =>
            {
                Interlocked.Increment(ref exports);
                finished.Release();
                return Task.FromResult<string?>("D:\\data\\diagnostics\\report.json");
            });
        viewModel.DiagnosticsEnabled = true;

        viewModel.ExportCommand.Execute(null);
        await finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, exports);
    }

    [Fact]
    public async Task An_export_that_writes_nothing_leaves_the_screen_saying_what_it_said()
    {
        var viewModel = CreateViewModel(
            new InMemoryPrivacySettings(),
            export: (_, _, _) => Task.FromResult<string?>(null));
        viewModel.DiagnosticsEnabled = true;

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Null(viewModel.ExportedFileName);
        Assert.Equal("PrivacyStatusOn", viewModel.StatusKey);
    }

    [Fact]
    public async Task An_export_that_fails_says_so_instead_of_looking_like_success()
    {
        var viewModel = CreateViewModel(
            new InMemoryPrivacySettings(),
            export: (_, _, _) => Task.FromException<string?>(new IOException("the folder is gone")));
        viewModel.DiagnosticsEnabled = true;

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal("PrivacyStatusFailed", viewModel.StatusKey);
        Assert.Null(viewModel.ExportedFileName);
    }

    [Fact]
    public void A_preview_that_cannot_be_built_says_so_instead_of_doing_nothing()
    {
        var viewModel = new PrivacySettingsViewModel(
            new InMemoryPrivacySettings(),
            new AllowlistedDiagnosticsBuilder(),
            () => throw new InvalidOperationException("the machine cannot be described"),
            (_, _, _) => Task.FromResult<string?>(null),
            () => Noon,
            NetworkPurposeRegistry.Declared);
        viewModel.DiagnosticsEnabled = true;

        viewModel.PreviewCommand.Execute(null);

        Assert.Null(viewModel.PreviewJson);
        Assert.Equal("PrivacyStatusFailed", viewModel.StatusKey);
    }

    [Fact]
    public void Setting_the_switch_to_what_it_already_is_changes_nothing()
    {
        var settings = new InMemoryPrivacySettings();
        var viewModel = CreateViewModel(settings);
        viewModel.DiagnosticsEnabled = true;
        var granted = settings.Current.GrantedUtc;

        viewModel.DiagnosticsEnabled = true;

        Assert.Equal(granted, settings.Current.GrantedUtc);
        Assert.Equal("PrivacyStatusOn", viewModel.StatusKey);
    }

    /// <summary>
    /// A button bound to a command asks once and waits to be told. Without the event the export button
    /// stays disabled for the life of the screen, whatever the switch says.
    /// </summary>
    [Fact]
    public void Turning_the_switch_tells_the_surface_the_export_became_possible()
    {
        var viewModel = CreateViewModel(new InMemoryPrivacySettings());
        var notifications = 0;
        viewModel.ExportCommand.CanExecuteChanged += (_, _) => notifications++;

        Assert.False(viewModel.ExportCommand.CanExecute(null));
        viewModel.DiagnosticsEnabled = true;

        Assert.True(viewModel.ExportCommand.CanExecute(null));
        Assert.Equal(1, notifications);

        viewModel.DiagnosticsEnabled = false;
        Assert.False(viewModel.ExportCommand.CanExecute(null));
        Assert.Equal(2, notifications);
    }

    [Fact]
    public void The_network_purposes_are_listed_so_a_person_can_read_them()
    {
        var viewModel = CreateViewModel(new InMemoryPrivacySettings());

        Assert.NotEmpty(viewModel.NetworkPurposes);
        Assert.All(viewModel.NetworkPurposes, purpose => Assert.False(string.IsNullOrWhiteSpace(purpose.Host)));
        Assert.All(viewModel.NetworkPurposes, purpose => Assert.False(string.IsNullOrWhiteSpace(purpose.Reason)));
    }

    [Fact]
    public void Every_visible_string_comes_from_the_resource_dictionary()
    {
        var presentationRoot = RepositoryLayout.PathFromRoot("src", "ApSolutions.LocalMedia.Presentation");
        var spanish = LoadResourceKeys(Path.Combine(presentationRoot, "Resources", "Strings.es.axaml"));
        foreach (var view in new[] { "PrivacySettingsView.axaml", "DiagnosticsPreviewView.axaml" })
        {
            var document = XDocument.Load(Path.Combine(presentationRoot, "Settings", view));
            var literals = document.Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "Header")
                .Select(attribute => attribute.Value)
                .Where(value => !value.StartsWith('{'))
                .ToArray();
            Assert.Empty(literals);
        }

        foreach (var key in new[]
        {
            "PrivacyTitle",
            "PrivacyDescription",
            "PrivacyDiagnosticsLabel",
            "PrivacyStatusOff",
            "PrivacyStatusOn",
            "PrivacyStatusExported",
            "PrivacyStatusFailed",
            "PrivacyPreviewLabel",
            "PrivacyExportLabel",
            "PrivacyNetworkTitle",
            "PrivacyOfflineNotice",
            "DiagnosticsPreviewTitle",
            "DiagnosticsPreviewDescription",
        })
        {
            Assert.Contains(key, spanish);
        }
    }

    /// <summary>
    /// LIB-016. The automatic refresh is subordinate to the consented connection: with no token in
    /// place the provider can only serve what it already cached, so a switch would offer something
    /// that cannot happen. It is not disabled — it is not there.
    /// </summary>
    [Fact]
    public void The_automatic_refresh_is_not_offered_without_a_consented_connection()
    {
        var refresh = new InMemoryAutoRefresh();
        var withoutConsent = CreateViewModel(new InMemoryPrivacySettings(), autoRefresh: refresh);
        var withConsent = CreateViewModel(
            new InMemoryPrivacySettings(),
            autoRefresh: refresh,
            hasConsentedConnection: true);

        Assert.False(withoutConsent.CanRefreshAutomatically);
        Assert.True(withConsent.CanRefreshAutomatically);
    }

    [Fact]
    public void The_automatic_refresh_starts_switched_off_and_remembers_being_turned_on()
    {
        var refresh = new InMemoryAutoRefresh();
        var viewModel = CreateViewModel(
            new InMemoryPrivacySettings(),
            autoRefresh: refresh,
            hasConsentedConnection: true);

        Assert.False(viewModel.AutomaticRefreshEnabled);
        Assert.False(refresh.AutomaticRefreshEnabled);

        viewModel.AutomaticRefreshEnabled = true;

        Assert.True(refresh.AutomaticRefreshEnabled);
        Assert.True(viewModel.AutomaticRefreshEnabled);
    }

    private sealed class InMemoryAutoRefresh : Application.Metadata.IAutoRefreshSettings
    {
        public bool AutomaticRefreshEnabled { get; private set; }

        public void SetAutomaticRefreshEnabled(bool enabled) => AutomaticRefreshEnabled = enabled;
    }

    private static PrivacySettingsViewModel CreateViewModel(
        InMemoryPrivacySettings settings,
        Func<DiagnosticsConsent, DiagnosticsInputs, CancellationToken, Task<string?>>? export = null,
        Application.Metadata.IAutoRefreshSettings? autoRefresh = null,
        bool hasConsentedConnection = false) =>
        new(
            settings,
            new AllowlistedDiagnosticsBuilder(),
            Inputs,
            export ?? ((_, _, _) => Task.FromResult<string?>(null)),
            () => Noon,
            NetworkPurposeRegistry.Declared,
            discard: null,
            autoRefresh,
            () => hasConsentedConnection);

    private static DiagnosticsInputs Inputs() => new(
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

    private static HashSet<string> LoadResourceKeys(string path)
    {
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        return [.. XDocument.Load(path)
            .Descendants()
            .Select(element => element.Attribute(xNamespace + "Key")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)];
    }

    private sealed class InMemoryPrivacySettings : IPrivacySettings
    {
        public DiagnosticsConsent Current { get; private set; } = new(IsGranted: false, GrantedUtc: null);

        public void Save(DiagnosticsConsent consent) => Current = consent;
    }
}
