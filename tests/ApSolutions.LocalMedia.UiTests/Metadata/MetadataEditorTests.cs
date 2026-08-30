// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Metadata;

public sealed class MetadataEditorTests
{
    [AvaloniaFact]
    public void Editor_is_bilingual_accessible_and_exposes_explicit_locks_alt_text_and_provider_restore()
    {
        var catalog = new CatalogMetadata(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
            new EditableMetadata(
                "La llegada",
                "Arrival",
                "Resumen",
                2016,
                ["Ciencia ficción"],
                "/poster.jpg",
                "/backdrop.jpg",
                null,
                new HashSet<MetadataField> { MetadataField.Title }),
            Revision: 2);
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            Assert.NotNull(Avalonia.Application.Current);
            App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo(cultureName));
            var view = new MetadataEditorView { DataContext = viewModel };
            var window = new Window { Width = 900, Height = 700, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var editableControls = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control is TextBox or CheckBox or Button)
                .ToArray();
            Assert.NotEmpty(editableControls);
            Assert.All(editableControls, control =>
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))));
            Assert.Equal(Enum.GetValues<MetadataField>().Length, view.GetVisualDescendants().OfType<CheckBox>().Count());
            Assert.NotNull(view.FindControl<TextBox>("ArtworkAlternativeText"));
            Assert.NotNull(view.FindControl<Button>("RestoreProviderMetadata"));

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var artifactPath = Path.Combine(
                RepositoryLayout.Root,
                "artifacts",
                "ui-captures",
                "T16",
                $"metadata-editor-{cultureName}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
            frame.Save(artifactPath, PngBitmapEncoderOptions.Default);
            window.Close();
        }
    }

    [Fact]
    public void Artwork_requires_alternative_text_for_local_and_remote_choices()
    {
        var picker = new ArtworkPickerViewModel
        {
            SelectedPersonalPath = "C:\\Pictures\\poster.jpg",
        };

        Assert.False(picker.CanApply);
        picker.AlternativeText = "Póster de La llegada";
        Assert.True(picker.CanApply);
        picker.SelectedPersonalPath = null;
        picker.SelectedRemoteUri = new Uri("https://image.tmdb.org/t/p/w500/poster.jpg");
        Assert.True(picker.CanApply);
    }

    [Fact]
    public void Editor_commands_save_refresh_restore_provider_fields_and_surface_conflicts()
    {
        var catalog = Catalog();
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        viewModel.Overview = "Resumen manual";
        viewModel.LockOverview = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Resumen manual", repository.Value.Metadata.Overview);
        Assert.Contains(MetadataField.Overview, repository.Value.Metadata.LockedFields);
        Assert.Equal(3, repository.Value.Revision);

        viewModel.RefreshProviderCommand.Execute(null);

        Assert.Equal("La llegada", repository.Value.Metadata.Title);
        Assert.Equal("Resumen manual", repository.Value.Metadata.Overview);
        Assert.Equal("/provider-backdrop.jpg", repository.Value.Metadata.BackdropPath);

        viewModel.RestoreProviderCommand.Execute(null);

        Assert.Equal("Arrival restored", repository.Value.Metadata.Title);
        Assert.Equal("Provider overview", repository.Value.Metadata.Overview);
        Assert.Empty(repository.Value.Metadata.LockedFields);

        repository.ForceConflict = true;
        viewModel.Title = "Conflicting title";
        viewModel.SaveCommand.Execute(null);
        Assert.True(viewModel.HasConflict);
    }

    /// <summary>
    /// The editor the application actually builds must be able to refresh from the provider.
    /// </summary>
    /// <remarks>
    /// Every other test of the refresh assigns <c>ProviderMetadata</c> itself — and that assignment
    /// is the only one in the whole repository. <c>CompositionRoot</c> builds this view model from a
    /// catalogue row, two use cases and the artwork picker, and nothing ever fills that property, so
    /// the two provider buttons are reachable, enabled, and cannot do anything. The suite stayed
    /// green because the double filled exactly the hole production has. This test builds the editor
    /// the way the application does and asks for the refresh, with nothing filled in by hand.
    /// </remarks>
    [Fact]
    public void The_editor_the_application_builds_can_refresh_from_the_provider()
    {
        var catalog = Catalog();
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        viewModel.RefreshProviderCommand.Execute(null);

        Assert.NotEqual(catalog, repository.Value);
    }

    /// <summary>
    /// A title nobody identified refreshes nothing, and says why. This test used to assert that a
    /// view model with no <c>ProviderMetadata</c> filled in was a safe no-op — describing as a
    /// deliberate guard the exact state the built application lived in permanently.
    /// </summary>
    [Fact]
    public void An_unidentified_title_refreshes_nothing_and_says_so()
    {
        var catalog = Unidentified();
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        viewModel.RefreshProviderCommand.Execute(null);
        viewModel.RestoreProviderCommand.Execute(null);

        Assert.Equal(catalog, repository.Value);
        Assert.False(viewModel.HasConflict);
        Assert.True(viewModel.IsUnidentified);
    }

    /// <summary>
    /// Identified, but the provider has nothing to give — the shipped default, with no token and
    /// therefore nothing but the cache. It is a state to explain, not a failure to report.
    /// </summary>
    [Fact]
    public void An_identified_title_the_provider_cannot_answer_for_says_that_instead()
    {
        var catalog = Catalog();
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            new RefreshMetadata(
                repository,
                new SilentTmdb(),
                new MetadataMergePolicy(),
                Language,
                TimeProvider.System),
            new ArtworkPickerViewModel());

        viewModel.RefreshProviderCommand.Execute(null);

        Assert.Equal(catalog, repository.Value);
        Assert.False(viewModel.IsUnidentified);
        Assert.True(viewModel.HasNoProviderAnswer);
    }

    private const string ProviderKey = "movie:6289";

    private static readonly MetadataLanguage Language = new("es-ES", "en-US");

    /// <summary>The refresh as the composition root builds it: resolving through the provider.</summary>
    private static RefreshMetadata Refresh(ICatalogMetadataRepository repository) => new(
        repository,
        new AnsweringTmdb(ProviderMetadata()),
        new MetadataMergePolicy(),
        Language,
        TimeProvider.System);

    /// <summary>A row nobody identified: no provider, no key, nothing to refresh against.</summary>
    private static CatalogMetadata Unidentified() => Catalog() with { Provider = null, ProviderKey = null };

    private static CatalogMetadata Catalog() => new(
        new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000001")),
        new EditableMetadata(
            "La llegada",
            "Arrival",
            "Resumen",
            2016,
            ["Ciencia ficción"],
            "/poster.jpg",
            "/backdrop.jpg",
            null,
            new HashSet<MetadataField> { MetadataField.Title }),
        Revision: 2,
        Provider: "tmdb",
        ProviderKey: ProviderKey);

    private static MetadataDetails ProviderMetadata() => new(
        new MetadataReference("tmdb", ProviderKey, MetadataContentKind.Movie),
        "Arrival restored",
        "Arrival",
        "Provider overview",
        2016,
        ["Science fiction"],
        "/provider-poster.jpg",
        "/provider-backdrop.jpg",
        TrailerKey: null);

    private sealed class AnsweringTmdb(MetadataDetails details) : IMetadataProvider
    {
        public string Name => "tmdb";

        public MetadataReference? TryCreateReference(string key) =>
            new(Name, key, MetadataContentKind.Movie);

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataSearchResult>>([]);

        public Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MetadataDetails?>(details);
    }

    private sealed class SilentTmdb : IMetadataProvider
    {
        public string Name => "tmdb";

        public MetadataReference? TryCreateReference(string key) =>
            new(Name, key, MetadataContentKind.Movie);

        public Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(
            MetadataSearchQuery query,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MetadataSearchResult>>([]);

        public Task<MetadataDetails?> GetDetailsAsync(
            MetadataReference reference,
            MetadataLanguage language,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MetadataDetails?>(null);
    }

    private sealed class UiMetadataRepository(CatalogMetadata initial) : ICatalogMetadataRepository
    {
        private CatalogMetadata _value = initial;

        public CatalogMetadata Value => _value;

        public bool ForceConflict { get; set; }

        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogMetadata?>(_value.TitleId == titleId ? _value : null);

        public Task<IReadOnlyList<CatalogMetadata>> ListStaleAsync(
            DateTimeOffset staleBefore,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogMetadata>>([]);

        public Task<MetadataWriteResult> TrySaveAsync(CatalogMetadata catalog, int expectedRevision, CancellationToken cancellationToken = default)
        {
            if (ForceConflict || _value.Revision != expectedRevision)
            {
                return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Conflict, _value));
            }

            _value = catalog with { Revision = expectedRevision + 1 };
            return Task.FromResult(new MetadataWriteResult(MetadataWriteOutcome.Applied, _value));
        }
    }
}
