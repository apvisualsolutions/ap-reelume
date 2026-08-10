// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

public sealed class MetadataMergePolicyTests
{
    [Fact]
    public void Remote_refresh_preserves_every_user_locked_field()
    {
        var current = new EditableMetadata(
            Title: "Mi título",
            OriginalTitle: "Arrival",
            Overview: "Resumen anterior",
            ReleaseYear: 2016,
            Genres: ["Drama"],
            PosterPath: "/manual-poster.jpg",
            BackdropPath: "/old-backdrop.jpg",
            LockedFields: new HashSet<MetadataField> { MetadataField.Title, MetadataField.PosterPath });
        var remote = Details(
            title: "La llegada",
            overview: "Nuevo resumen",
            genres: ["Ciencia ficción"],
            poster: "/remote-poster.jpg",
            backdrop: "/remote-backdrop.jpg");

        var merged = new MetadataMergePolicy().Merge(current, remote);

        Assert.Equal("Mi título", merged.Title);
        Assert.Equal("/manual-poster.jpg", merged.PosterPath);
        Assert.Equal("Nuevo resumen", merged.Overview);
        Assert.Equal(["Ciencia ficción"], merged.Genres);
        Assert.Equal("/remote-backdrop.jpg", merged.BackdropPath);
        Assert.Equal(current.LockedFields, merged.LockedFields);
    }

    [Fact]
    public void Missing_remote_optional_values_do_not_erase_local_metadata()
    {
        var current = new EditableMetadata(
            "Arrival",
            "Arrival",
            "Local overview",
            2016,
            ["Drama"],
            "/poster.jpg",
            "/backdrop.jpg",
            new HashSet<MetadataField>());
        var remote = Details(
            title: "La llegada",
            overview: null,
            genres: [],
            poster: null,
            backdrop: null);

        var merged = new MetadataMergePolicy().Merge(current, remote);

        Assert.Equal("La llegada", merged.Title);
        Assert.Equal("Local overview", merged.Overview);
        Assert.Equal(["Drama"], merged.Genres);
        Assert.Equal("/poster.jpg", merged.PosterPath);
        Assert.Equal("/backdrop.jpg", merged.BackdropPath);
    }

    [Fact]
    public void Locking_all_fields_makes_remote_refresh_non_destructive()
    {
        var allFields = Enum.GetValues<MetadataField>().ToHashSet();
        var current = new EditableMetadata(
            "Manual",
            "Manual original",
            "Manual overview",
            2001,
            ["Manual genre"],
            "/manual-poster.jpg",
            "/manual-backdrop.jpg",
            allFields);

        var merged = new MetadataMergePolicy().Merge(
            current,
            Details(
                "Remote",
                "Remote overview",
                ["Remote genre"],
                "/remote-poster.jpg",
                "/remote-backdrop.jpg"));

        Assert.Equal(current, merged);
    }

    [Fact]
    public void Null_documents_are_rejected()
    {
        var policy = new MetadataMergePolicy();
        var details = Details("Remote", "Overview", ["Drama"], null, null);
        var current = new EditableMetadata(
            "Local",
            null,
            null,
            null,
            [],
            null,
            null,
            new HashSet<MetadataField>());

        Assert.Throws<ArgumentNullException>(() => policy.Merge(null!, details));
        Assert.Throws<ArgumentNullException>(() => policy.Merge(current, null!));
    }

    private static MetadataDetails Details(
        string title,
        string? overview,
        IReadOnlyList<string> genres,
        string? poster,
        string? backdrop) => new(
            new MetadataReference("tmdb", "movie:329865", MetadataContentKind.Movie),
            "es-ES",
            title,
            "Arrival",
            overview,
            2016,
            genres,
            poster,
            backdrop);
}
