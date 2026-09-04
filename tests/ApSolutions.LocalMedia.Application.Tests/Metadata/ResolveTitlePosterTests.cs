// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Metadata;

/// <summary>
/// The rule that turns what a title stores about its cover into the file that draws it.
/// </summary>
/// <remarks>
/// <b>It had no test at all until 2026-09-04, and that is the point of this file.</b> The rule lived
/// as a private method inside the composition root, so nothing could reach it — and it is the rule
/// that decides whether somebody sees their own cover. It moved out because the library grid needs
/// the same answer the detail cards need, and the only thing worse than an untested rule is two
/// copies of it.
/// </remarks>
public sealed class ResolveTitlePosterTests
{
    private static readonly TitleId Title = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    [Fact]
    public void A_provider_address_is_looked_up_where_downloaded_artwork_lives()
    {
        var store = new StubStore { RemoteAnswer = "cache/artwork/abc/poster.jpg" };

        var found = new ResolveTitlePoster(store).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
        Assert.Equal(Title, store.LastRemoteTitle);
        Assert.Equal(0, store.PersonalCalls);
    }

    [Fact]
    public void A_personal_cover_is_looked_up_where_chosen_artwork_lives()
    {
        var store = new StubStore { PersonalAnswer = "personal-artwork/abc/cover.png" };
        var chosen = new string('a', 64) + ".png";

        var found = new ResolveTitlePoster(store).Find(
            Title,
            Path.Combine("C:", "anywhere", "personal-artwork", chosen));

        Assert.Equal("personal-artwork/abc/cover.png", found);
        Assert.Equal(chosen, store.LastPersonalCover);
        Assert.Equal(0, store.RemoteCalls);
    }

    /// <summary>
    /// The order, asserted rather than assumed: a title carrying a provider address gets the
    /// provider's picture even when the store would have answered for a personal one too.
    /// </summary>
    [Fact]
    public void The_provider_is_asked_first_when_the_stored_value_is_one_of_its_addresses()
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };

        var found = new ResolveTitlePoster(store).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
        Assert.Equal(0, store.PersonalCalls);
    }

    /// <summary>
    /// A stored value of neither shape answers nothing, and asks the store nothing.
    /// </summary>
    /// <remarks>
    /// That field is free text. Reading an arbitrary path out of it would turn a metadata editor
    /// into a reader of any file on the machine, which is why a hand-typed path has never drawn.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("C:\\Users\\someone\\Pictures\\holiday.jpg")]
    [InlineData("../../etc/passwd")]
    [InlineData("not a path at all")]
    public void A_value_that_names_neither_shape_draws_nothing_and_asks_nothing(string? stored)
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };

        Assert.Null(new ResolveTitlePoster(store).Find(Title, stored));
        Assert.Equal(0, store.RemoteCalls);
        Assert.Equal(0, store.PersonalCalls);
    }

    /// <summary>
    /// A field that names a picture the disk does not have is no picture, not a broken one.
    /// </summary>
    [Fact]
    public void A_named_picture_that_is_not_on_this_disk_is_no_picture()
    {
        var store = new StubStore();

        Assert.Null(new ResolveTitlePoster(store).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg"));
        Assert.Equal(1, store.RemoteCalls);
    }

    [Fact]
    public void A_resolver_without_a_store_is_refused_where_it_is_built()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ResolveTitlePoster(null!));
    }

    private sealed class StubStore : IArtworkStore
    {
        public string? RemoteAnswer { get; init; }

        public string? PersonalAnswer { get; init; }

        public int RemoteCalls { get; private set; }

        public int PersonalCalls { get; private set; }

        public TitleId LastRemoteTitle { get; private set; }

        public string? LastPersonalCover { get; private set; }

        public string? Find(TitleId titleId, Uri source)
        {
            RemoteCalls++;
            LastRemoteTitle = titleId;
            return RemoteAnswer;
        }

        public string? FindPersonal(TitleId titleId, string coverFileName)
        {
            PersonalCalls++;
            LastPersonalCover = coverFileName;
            return PersonalAnswer;
        }

        public Task<string?> FetchAsync(
            TitleId titleId,
            Uri source,
            string alternativeText,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<ArtworkReference> ImportPersonalAsync(
            TitleId titleId,
            string sourcePath,
            string alternativeText,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
