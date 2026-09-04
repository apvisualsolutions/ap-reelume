// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Text;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

public sealed class ArtworkCacheTests
{
    [Fact]
    public async Task Personal_artwork_is_copied_to_exportable_storage_and_remote_art_is_regenerable()
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, "chosen-poster.png");
        await File.WriteAllBytesAsync(source, [1, 2, 3, 4], TestContext.Current.CancellationToken);
        var handler = new ArtworkHandler([Response(HttpStatusCode.OK, "remote-image")]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000001"));

        var personal = await cache.ImportPersonalAsync(
            titleId,
            source,
            "Póster elegido por la persona",
            TestContext.Current.CancellationToken);
        var remote = await cache.CacheRemoteAsync(
            titleId,
            new Uri("https://image.tmdb.org/t/p/w500/poster.jpg"),
            "Póster de La llegada",
            previous: personal,
            TestContext.Current.CancellationToken);
        var exportable = await cache.GetExportablePersonalArtworkAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ArtworkOrigin.Personal, personal.Origin);
        Assert.True(personal.IsExportable);
        Assert.Contains("personal-artwork", personal.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(personal.Path));
        Assert.Equal(ArtworkOrigin.RemoteCache, remote.Origin);
        Assert.False(remote.IsExportable);
        Assert.Contains(Path.Combine("cache", "artwork"), remote.Path, StringComparison.Ordinal);
        Assert.True(File.Exists(remote.Path));
        Assert.Equal([personal.Path], exportable);
        Assert.DoesNotContain(remote.Path, exportable);
    }

    [Fact]
    public async Task Failed_remote_download_preserves_previous_artwork()
    {
        using var directory = new DatabaseTestDirectory();
        var handler = new ArtworkHandler([Response(HttpStatusCode.ServiceUnavailable, "offline")]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var previous = new ArtworkReference(
            Path.Combine(directory.Path, "personal-artwork", "existing.png"),
            ArtworkOrigin.Personal,
            "Texto alternativo",
            IsExportable: true);

        var result = await cache.CacheRemoteAsync(
            new TitleId(Guid.NewGuid()),
            new Uri("https://image.tmdb.org/t/p/w500/missing.jpg"),
            "Póster remoto",
            previous,
            TestContext.Current.CancellationToken);

        Assert.Equal(previous, result);
    }

    [Fact]
    public async Task Clearing_remote_cache_preserves_personal_art_and_remote_can_regenerate()
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, "chosen.png");
        await File.WriteAllBytesAsync(source, [7, 8, 9], TestContext.Current.CancellationToken);
        var handler = new ArtworkHandler([
            Response(HttpStatusCode.OK, "first"),
            Response(HttpStatusCode.OK, "second"),
        ]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var titleId = new TitleId(Guid.NewGuid());
        var personal = await cache.ImportPersonalAsync(titleId, source, "Arte personal", TestContext.Current.CancellationToken);
        var firstRemote = await cache.CacheRemoteAsync(
            titleId,
            new Uri("https://image.tmdb.org/t/p/w500/regenerate.jpg"),
            "Arte remoto",
            personal,
            TestContext.Current.CancellationToken);

        await cache.ClearRemoteCacheAsync(TestContext.Current.CancellationToken);

        Assert.True(File.Exists(personal.Path));
        Assert.False(File.Exists(firstRemote.Path));
        var regenerated = await cache.CacheRemoteAsync(
            titleId,
            new Uri("https://image.tmdb.org/t/p/w500/regenerate.jpg"),
            "Arte remoto",
            personal,
            TestContext.Current.CancellationToken);
        Assert.True(File.Exists(regenerated.Path));
        Assert.Equal("second", await File.ReadAllTextAsync(regenerated.Path, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// SEC-004: artwork addresses come from provider metadata, and the promise only covers the
    /// declared host. An address anywhere else is refused before a single byte is asked for.
    /// </summary>
    [Fact]
    public async Task Artwork_from_an_undeclared_host_is_refused_without_a_request()
    {
        using var directory = new DatabaseTestDirectory();
        var handler = new ArtworkHandler([]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);

        _ = await Assert.ThrowsAsync<HttpRequestException>(() => cache.CacheRemoteAsync(
            new TitleId(Guid.NewGuid()),
            new Uri("https://cdn.example.net/poster.jpg"),
            "Póster remoto",
            previous: null,
            TestContext.Current.CancellationToken));

        Assert.Equal(0, handler.Requests);
    }

    /// <summary>
    /// SEC-005: a poster bigger than the ceiling is refused mid-stream, and whatever artwork the
    /// title already had stays in place.
    /// </summary>
    [Fact]
    public async Task Oversized_artwork_is_refused_and_the_previous_artwork_survives()
    {
        using var directory = new DatabaseTestDirectory();
        var oversized = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[ArtworkCache.MaximumArtworkBytes + 1]),
        };
        var handler = new ArtworkHandler([oversized]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var previous = new ArtworkReference(
            Path.Combine(directory.Path, "personal-artwork", "existing.png"),
            ArtworkOrigin.Personal,
            "Texto alternativo",
            IsExportable: true);

        var kept = await cache.CacheRemoteAsync(
            new TitleId(Guid.NewGuid()),
            new Uri("https://image.tmdb.org/t/p/original/enormous.jpg"),
            "Póster remoto",
            previous,
            TestContext.Current.CancellationToken);

        Assert.Equal(previous, kept);
    }

    /// <summary>
    /// The two members the film card and the identification use: look on the disk, and fetch.
    /// </summary>
    /// <remarks>
    /// <c>Find</c> is what makes it possible for a card to draw a poster without opening a
    /// connection, so what is asserted is that it answers before anything has been fetched (nothing),
    /// after (the file), and that fetching a second time is not a second request — the card is opened
    /// far more often than a title is identified.
    /// </remarks>
    [Fact]
    public async Task What_is_on_the_disk_is_found_without_a_request_and_fetched_only_once()
    {
        using var directory = new DatabaseTestDirectory();
        var handler = new ArtworkHandler([Response(HttpStatusCode.OK, "remote-image")]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000009"));
        var source = new Uri("https://image.tmdb.org/t/p/w780/wXsQ.jpg");

        Assert.Null(cache.Find(titleId, source));
        Assert.Equal(0, handler.Requests);

        var fetched = await cache.FetchAsync(
            titleId,
            source,
            "Póster de La llegada",
            TestContext.Current.CancellationToken);

        Assert.NotNull(fetched);
        Assert.True(File.Exists(fetched));
        Assert.Equal(fetched, cache.Find(titleId, source));
        Assert.Equal(1, handler.Requests);

        // A different address for the same title is a different file, which is what lets a poster
        // change without the old one being served from under it.
        Assert.Null(cache.Find(titleId, new Uri("https://image.tmdb.org/t/p/w780/other.jpg")));
        Assert.Equal(1, handler.Requests);
    }

    /// <summary>
    /// Everything <c>CacheRemoteAsync</c> refuses becomes "no artwork" rather than an exception.
    /// </summary>
    /// <remarks>
    /// Both refusals are worth having and neither is worth stopping an identification for: a title
    /// whose poster could not be had is a title identified. The host refusal is the policy one — it
    /// never reaches the network at all, which is why the request count is asserted beside it.
    /// </remarks>
    [Fact]
    public async Task A_refusal_answers_no_artwork_rather_than_throwing()
    {
        using var directory = new DatabaseTestDirectory();
        var handler = new ArtworkHandler([Response(HttpStatusCode.ServiceUnavailable, "offline")]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-000000000010"));

        var undeclared = await cache.FetchAsync(
            titleId,
            new Uri("https://evil.example/t/p/w780/wXsQ.jpg"),
            "Póster",
            TestContext.Current.CancellationToken);

        Assert.Null(undeclared);
        Assert.Equal(0, handler.Requests);

        var refused = await cache.FetchAsync(
            titleId,
            new Uri("https://image.tmdb.org/t/p/w780/wXsQ.jpg"),
            "Póster",
            TestContext.Current.CancellationToken);

        Assert.Null(refused);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public void Looking_for_artwork_needs_an_address_to_look_for()
    {
        using var directory = new DatabaseTestDirectory();
        using var client = new HttpClient(new ArtworkHandler([]));
        var cache = new ArtworkCache(directory.Path, client);

        Assert.Throws<ArgumentNullException>(() => cache.Find(new TitleId(Guid.Empty), null!));
    }

    /// <summary>
    /// A cover the allow-list refuses is refused by the store itself, and copies nothing.
    /// </summary>
    /// <remarks>
    /// <b>The check is here and not only in the use case above it.</b> This is the line that reads
    /// somebody's file into the application's own data — data the backup then carries off the
    /// machine — so a guard that lived only on the caller would protect the callers that exist
    /// today and none of the ones written next. The three refusals are the three ways a chosen file
    /// can be wrong: it is not there any more, it is not an image at all, or it is larger than the
    /// ceiling a personal cover is allowed.
    /// </remarks>
    [Theory]
    [InlineData("gone.png", -1)]
    [InlineData("a-video.mkv", 4)]
    [InlineData("enormous.png", (11 * 1024 * 1024))]
    public async Task A_cover_the_allow_list_refuses_never_reaches_the_application_data(
        string name,
        int sizeInBytes)
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, name);
        if (sizeInBytes >= 0)
        {
            await File.WriteAllBytesAsync(
                source,
                new byte[sizeInBytes],
                TestContext.Current.CancellationToken);
        }

        var handler = new ArtworkHandler([]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ImportPersonalAsync(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-00000000000f")),
            source,
            "Una portada que no vale",
            TestContext.Current.CancellationToken));

        Assert.Empty(await cache.GetExportablePersonalArtworkAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, handler.Requests);
    }

    /// <summary>
    /// A cached poster is named after the kind the provider said it was, and anything else is a JPEG.
    /// </summary>
    /// <remarks>
    /// <b>The extension cannot be read off the address</b> — TMDB's paths end in <c>.jpg</c> whatever
    /// they actually hold — so it comes from the media type, and getting it wrong means a file the
    /// image decoder opens by name and refuses. The unknown kind falls back rather than refusing:
    /// what arrived is a picture the provider offered, and a header nobody recognises is not a
    /// reason to leave a card blank.
    /// </remarks>
    [Theory]
    [InlineData("image/png", ".png")]
    [InlineData("image/webp", ".webp")]
    [InlineData("application/octet-stream", ".jpg")]
    public async Task A_cached_poster_is_named_after_the_kind_the_provider_said_it_was(
        string mediaType,
        string extension)
    {
        using var directory = new DatabaseTestDirectory();
        var handler = new ArtworkHandler([Response(HttpStatusCode.OK, "remote-image", mediaType)]);
        using var client = new HttpClient(handler);
        var cache = new ArtworkCache(directory.Path, client);

        var remote = await cache.CacheRemoteAsync(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000a1")),
            new Uri("https://image.tmdb.org/t/p/w500/poster.jpg"),
            "Póster remoto",
            previous: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(extension, Path.GetExtension(remote.Path));
        Assert.True(File.Exists(remote.Path));
    }

    /// <summary>
    /// What the picker imported is found again from what the editor stored, which is the whole of
    /// the defect closed on 2026-09-04: the file was written, the path was kept, and nothing ever
    /// asked for it back.
    /// </summary>
    [Fact]
    public async Task A_cover_that_was_imported_is_found_again_from_the_stored_path()
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, "elegida.png");
        await File.WriteAllBytesAsync(source, [9, 8, 7, 6], TestContext.Current.CancellationToken);
        using var client = new HttpClient(new ArtworkHandler([]));
        var cache = new ArtworkCache(directory.Path, client);
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000a1"));

        var imported = await cache.ImportPersonalAsync(
            titleId,
            source,
            "Portada elegida",
            TestContext.Current.CancellationToken);

        Assert.Equal(imported.Path, cache.FindPersonal(titleId, Path.GetFileName(imported.Path)));

        // Somebody else's title does not get this cover, because the folder is composed from the
        // title asked for and not from anything the stored value said.
        Assert.Null(cache.FindPersonal(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000a2")),
            Path.GetFileName(imported.Path)));
    }

    /// <summary>
    /// A backup restored onto another machine puts the images under that machine's own folder while
    /// the stored value still names the old one. The cover is found anyway, because the folder was
    /// never the part that was read.
    /// </summary>
    [Fact]
    public async Task A_cover_restored_under_a_different_data_root_is_still_found()
    {
        using var origin = new DatabaseTestDirectory();
        using var restored = new DatabaseTestDirectory();
        var source = Path.Combine(origin.Path, "elegida.png");
        await File.WriteAllBytesAsync(source, [4, 5, 6, 7], TestContext.Current.CancellationToken);
        using var client = new HttpClient(new ArtworkHandler([]));
        var titleId = new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000b1"));

        var before = await new ArtworkCache(origin.Path, client).ImportPersonalAsync(
            titleId,
            source,
            "Portada elegida",
            TestContext.Current.CancellationToken);

        // What the restore does: the same relative shape under the new machine's data root.
        var name = Path.GetFileName(before.Path);
        var folder = Path.Combine(restored.Path, "personal-artwork", titleId.Value.ToString("N"));
        Directory.CreateDirectory(folder);
        File.Copy(before.Path, Path.Combine(folder, name));

        var here = new ArtworkCache(restored.Path, client);

        // The value the database still holds is the path from the machine the backup was made on.
        Assert.NotEqual(before.Path, here.FindPersonal(titleId, Path.GetFileName(before.Path)));
        Assert.Equal(Path.Combine(folder, name), here.FindPersonal(titleId, name));
    }

    /// <summary>
    /// The guard is on the line that composes a path and not only on its caller, and this is what
    /// exercises it: a value that names a real file somewhere else reaches nothing, and the file it
    /// named is still sitting there afterwards, unread and unmoved.
    /// </summary>
    [Theory]
    [InlineData(@"..\..\..\secreto.png")]
    [InlineData(@"\\servidor\recurso\secreto.png")]
    [InlineData("secreto.png")]
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.exe")]
    [InlineData("9F2C4A1B8E7D6053F4A2B9C8D7E6F5A4B3C2D1E0F9A8B7C6D5E4F3A2B1C0D9E8.png")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_name_the_policy_refuses_reaches_no_file_at_all(string coverFileName)
    {
        using var directory = new DatabaseTestDirectory();
        var elsewhere = Path.Combine(directory.Path, "secreto.png");
        await File.WriteAllBytesAsync(elsewhere, [1, 1, 1, 1], TestContext.Current.CancellationToken);
        using var client = new HttpClient(new ArtworkHandler([]));
        var cache = new ArtworkCache(directory.Path, client);

        Assert.Null(cache.FindPersonal(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000c1")),
            coverFileName));

        // The assertion that keeps the one above from being blind: refusing must mean the file was
        // left alone, not that it happened to be missing.
        Assert.True(File.Exists(elsewhere), "the file the value named was not left where it was.");
    }

    /// <summary>
    /// A name of the right shape whose file is not there is «no cover», which is a state every
    /// surface already draws. Somebody who deleted the folder by hand gets their initials back.
    /// </summary>
    [Fact]
    public void A_well_formed_name_with_nothing_behind_it_is_no_cover()
    {
        using var directory = new DatabaseTestDirectory();
        using var client = new HttpClient(new ArtworkHandler([]));
        var cache = new ArtworkCache(directory.Path, client);

        Assert.Null(cache.FindPersonal(
            new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000d1")),
            "9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.png"));
    }

    private static HttpResponseMessage Response(
        HttpStatusCode status,
        string body,
        string mediaType = "image/jpeg") => new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, mediaType),
        };

    private sealed class ArtworkHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Assert.Equal("image.tmdb.org", request.RequestUri?.Host);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
