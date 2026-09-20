// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
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

            // LIB-021: the cover source is a row of options, and each row says the list's name out
            // loud plus its own choice in the help text — the shape the audio device list has. All
            // four are asserted here rather than in the layout tests, which show this view with no
            // data context and would find an empty list and pass.
            var options = view.GetVisualDescendants().OfType<RadioButton>().ToArray();
            Assert.Equal(1 + Enum.GetValues<CoverOrigin>().Length, options.Length);
            Assert.All(options, option =>
            {
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(option)));
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetHelpText(option)));
            });
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
    /// <summary>
    /// The editor files an imported cover in its own field and saves it there, leaving the provider's
    /// poster and its lock exactly as they were (LIB-021, ADR-0009).
    /// </summary>
    /// <remarks>
    /// <b>Until 2026-09-18 this test asserted the defect.</b> It was called «an imported cover reaches
    /// the poster field and locks it»: the chosen path overwrote the provider's field and set its lock,
    /// because that lock was the only thing standing between the choice and the next refresh — and
    /// «restore the provider's fields» clears every lock, so the choice was lost and its file orphaned.
    /// With its own field nothing needs locking, and the provider's poster is never touched.
    /// </remarks>
    [AvaloniaFact]
    public async Task An_imported_cover_is_saved_apart_and_leaves_the_provider_poster_alone()
    {
        var chosen = new string('e', 64) + ".png";
        var catalog = Unidentified() with
        {
            Metadata = Catalog().Metadata with
            {
                PosterPath = "/provider-poster.jpg",
                LockedFields = new HashSet<MetadataField>(),
            },
        };

        var repository = new UiMetadataRepository(catalog);
        var picker = new ArtworkPickerViewModel(
            _ => Task.FromResult<string?>(Path.Combine("C:", "arte", "portada.png")),
            (_, _, _, _) => Task.FromResult(new PersonalCoverResult(
                CoverImageVerdict.Approved,
                Path.Combine("C:", "datos", "personal-artwork", "abc", chosen))));

        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            picker);

        Assert.Null(viewModel.PersonalCover);

        picker.ChooseCoverCommand.Execute(null);
        for (var i = 0; i < 200 && picker.IsChoosing; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(chosen, viewModel.PersonalCover);
        Assert.Equal("/provider-poster.jpg", viewModel.PosterPath);
        Assert.False(viewModel.LockPosterPath);

        viewModel.SaveCommand.Execute(null);
        for (var i = 0; i < 200 && repository.Value.Metadata.PersonalCover is null; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.Equal(chosen, repository.Value.Metadata.PersonalCover);
        Assert.Equal("/provider-poster.jpg", repository.Value.Metadata.PosterPath);
    }

    private static RefreshMetadata Refresh(ICatalogMetadataRepository repository) => new(
        repository,
        new AnsweringTmdb(ProviderMetadata()),
        new MetadataMergePolicy(),
        Language,
        TimeProvider.System);

    /// <summary>A row nobody identified: no provider, no key, nothing to refresh against.</summary>
    /// <summary>
    /// A write that lands says so, and one that does not stays quiet.
    /// </summary>
    /// <remarks>
    /// <b>This is what makes a chosen cover appear without leaving the title.</b> Closing the editor
    /// drops both surfaces and reloads nothing, so until 2026-09-04 somebody could choose a cover, be
    /// told «Portada puesta», save, and watch the card behind not change. The editor does not reach
    /// for the card: it says a write landed, and composition decides what to re-read.
    /// <para>
    /// The conflict half is the one worth having. Telling the card to re-read after a write that
    /// never happened would redraw exactly what is already there, and teach whoever is watching that
    /// the button did something.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void Only_a_write_that_landed_tells_whoever_is_drawing_the_title()
    {
        var catalog = Catalog();
        var repository = new UiMetadataRepository(catalog);
        var told = 0;
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel(),
            onApplied: () =>
            {
                told++;
                return Task.CompletedTask;
            });

        viewModel.Overview = "Resumen manual";
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, told);

        repository.ForceConflict = true;
        viewModel.Overview = "Otro resumen";
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewModel.HasConflict);
        Assert.Equal(1, told);
    }

    /// <summary>
    /// The editor that nobody handed a listener to saves exactly the same, which is the shape every
    /// other test of this class builds and the shape a test of the fields alone should keep.
    /// </summary>
    [AvaloniaFact]
    public void An_editor_with_nobody_listening_saves_all_the_same()
    {
        var catalog = Catalog();
        var repository = new UiMetadataRepository(catalog);
        var viewModel = new MetadataEditorViewModel(
            catalog,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        viewModel.Overview = "Sin nadie escuchando";
        viewModel.SaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Sin nadie escuchando", repository.Value.Metadata.Overview);
        Assert.False(viewModel.HasConflict);
    }

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

    /// <summary>
    /// LIB-021, ADR-0009 decision 4: this one title can override the general cover order, and can
    /// be put back on it.
    /// </summary>
    [AvaloniaFact]
    public void The_cover_source_of_one_title_is_chosen_and_can_be_put_back_on_the_general_order()
    {
        var repository = new UiMetadataRepository(CoverOrderCatalog(null));
        var editor = new MetadataEditorViewModel(
            repository.Value,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        // A title that never chose opens on the general order, which is the first choice offered.
        Assert.Equal(0, editor.CoverSourceIndex);
        Assert.Equal(1 + Enum.GetValues<CoverOrigin>().Length, editor.CoverSourceChoices.Count);

        editor.CoverSourceIndex = 1 + (int)CoverOrigin.Frame;
        editor.SaveCommand.Execute(null);

        // The whole order is stored with the chosen origin first, so moving the general order later
        // cannot change what this title was told to do.
        Assert.Equal("Frame,Personal,Provider", repository.Value.Metadata.CoverOrder);

        var reopened = new MetadataEditorViewModel(
            repository.Value,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());
        Assert.Equal(1 + (int)CoverOrigin.Frame, reopened.CoverSourceIndex);

        // Changing the choice on a title that already had one keeps the rest of the stored order
        // behind the new winner, rather than rebuilding it from the general one.
        reopened.CoverSourceIndex = 1 + (int)CoverOrigin.Provider;
        reopened.SaveCommand.Execute(null);
        Assert.Equal("Provider,Frame,Personal", repository.Value.Metadata.CoverOrder);

        var third = new MetadataEditorViewModel(
            repository.Value,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());
        third.CoverSourceIndex = 0;
        third.SaveCommand.Execute(null);

        Assert.Null(repository.Value.Metadata.CoverOrder);
    }

    /// <summary>
    /// A row whose stored order names nothing valid opens on the general order rather than on
    /// nothing, which is the same repair a hand-edited settings file gets.
    /// </summary>
    [AvaloniaFact]
    public void A_stored_order_that_names_nothing_valid_opens_on_the_general_order()
    {
        var repository = new UiMetadataRepository(CoverOrderCatalog("Nonsense"));

        var editor = new MetadataEditorViewModel(
            repository.Value,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());

        Assert.Equal(0, editor.CoverSourceIndex);
    }

    /// <summary>
    /// The command behind the cover source rows: it picks one, refuses anything that is not a row,
    /// and every row says when it becomes the chosen one — which is what draws the radio filled.
    /// </summary>
    [AvaloniaFact]
    public void Choosing_a_cover_source_refuses_anything_that_is_not_one_and_every_row_says_so()
    {
        var repository = new UiMetadataRepository(CoverOrderCatalog(null));
        var editor = new MetadataEditorViewModel(
            repository.Value,
            new UpdateMetadata(repository),
            Refresh(repository),
            new ArtworkPickerViewModel());
        var frame = editor.CoverSourceChoices.Single(choice => choice.Index == 1 + (int)CoverOrigin.Frame);
        var told = new List<string?>();
        frame.PropertyChanged += (_, args) => told.Add(args.PropertyName);

        Assert.False(editor.ChooseCoverSourceCommand.CanExecute(null));
        Assert.False(editor.ChooseCoverSourceCommand.CanExecute("Frame"));
        editor.ChooseCoverSourceCommand.Execute("not a choice");
        Assert.Equal(0, editor.CoverSourceIndex);
        Assert.Empty(told);

        Assert.True(editor.ChooseCoverSourceCommand.CanExecute(frame));
        editor.ChooseCoverSourceCommand.Execute(frame);

        Assert.Equal(frame.Index, editor.CoverSourceIndex);
        Assert.True(frame.IsSelected);
        Assert.Contains(nameof(CoverSourceOption.IsSelected), told);
        Assert.All(
            editor.CoverSourceChoices.Where(choice => choice.Index != frame.Index),
            choice => Assert.False(choice.IsSelected));

        // Choosing the same row again says nothing: a notification per click would redraw the whole
        // list every time somebody pressed what was already chosen.
        told.Clear();
        editor.ChooseCoverSourceCommand.Execute(frame);
        Assert.Empty(told);
    }

    private static CatalogMetadata CoverOrderCatalog(string? coverOrder) =>
        new(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000009")),
            new EditableMetadata(
                "La llegada",
                null,
                null,
                2016,
                [],
                "/poster.jpg",
                null,
                null,
                new HashSet<MetadataField>())
            {
                CoverOrder = coverOrder,
            },
            Revision: 0);

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
