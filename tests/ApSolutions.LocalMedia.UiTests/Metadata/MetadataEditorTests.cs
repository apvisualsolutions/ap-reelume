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
            new RefreshMetadata(repository, new MetadataMergePolicy()),
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
            new RefreshMetadata(repository, new MetadataMergePolicy()),
            new ArtworkPickerViewModel());

        viewModel.Overview = "Resumen manual";
        viewModel.LockOverview = true;
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Resumen manual", repository.Value.Metadata.Overview);
        Assert.Contains(MetadataField.Overview, repository.Value.Metadata.LockedFields);
        Assert.Equal(3, repository.Value.Revision);

        viewModel.ProviderMetadata = ProviderMetadata(catalog.TitleId);
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

    [Fact]
    public void Refresh_without_provider_metadata_is_a_safe_no_op()
    {
        var catalog = Catalog();
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            new RefreshMetadata(repository, new MetadataMergePolicy()),
            new ArtworkPickerViewModel());

        viewModel.RefreshProviderCommand.Execute(null);
        viewModel.RestoreProviderCommand.Execute(null);

        Assert.Equal(catalog, repository.Value);
        Assert.False(viewModel.HasConflict);
    }

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
        Revision: 2);

    private static MetadataDetails ProviderMetadata(TitleId titleId) => new(
        new MetadataReference("tmdb", $"movie:{titleId.Value:N}", MetadataContentKind.Movie),
        "es-ES",
        "Arrival restored",
        "Arrival",
        "Provider overview",
        2016,
        ["Science fiction"],
        "/provider-poster.jpg",
        "/provider-backdrop.jpg",
        TrailerKey: null);

    private sealed class UiMetadataRepository(CatalogMetadata initial) : ICatalogMetadataRepository
    {
        private CatalogMetadata _value = initial;

        public CatalogMetadata Value => _value;

        public bool ForceConflict { get; set; }

        public Task<CatalogMetadata?> GetAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CatalogMetadata?>(_value.TitleId == titleId ? _value : null);

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
