// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Metadata;
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

    /// <summary>A frames folder nobody ever wrote into.</summary>
    private static readonly FramePaths NoFrames = new(Path.Combine(Path.GetTempPath(), "no-title-frames-" + Guid.NewGuid().ToString("N")));

    /// <summary>The general order as it comes out of the box.</summary>
    private static readonly ICoverOrderSettings DefaultOrder = new StubOrder(CoverOrderPolicy.Default);

    /// <summary>
    /// ADR-0009's third origin: with nothing picked and nothing from the provider, the frame taken
    /// from the title's own video draws.
    /// </summary>
    [Fact]
    public void The_frame_draws_when_there_is_no_other_cover()
    {
        using var frames = FramePaths.WithFrameFor(Title);

        var found = NewResolver(new StubStore(), frames).Find(Title, posterPath: null);

        Assert.Equal(ResolveTitlePoster.FrameFileFor(frames, Title), found);
    }

    /// <summary>The frame is last: a provider poster on disk wins over it.</summary>
    [Fact]
    public void The_provider_wins_over_a_frame()
    {
        using var frames = FramePaths.WithFrameFor(Title);
        var store = new StubStore { RemoteAnswer = "cache/artwork/abc/poster.jpg" };

        var found = NewResolver(store, frames).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
    }

    /// <summary>A frame nobody took yet is no picture: resolving never decodes anything.</summary>
    [Fact]
    public void A_frame_not_taken_yet_is_no_picture()
    {
        Assert.Null(NewResolver(new StubStore(), NoFrames).Find(Title, posterPath: null));
    }

    [Fact]
    public void A_provider_address_is_looked_up_where_downloaded_artwork_lives()
    {
        var store = new StubStore { RemoteAnswer = "cache/artwork/abc/poster.jpg" };

        var found = NewResolver(store, NoFrames).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
        Assert.Equal(Title, store.LastRemoteTitle);
        Assert.Equal(0, store.PersonalCalls);
    }

    [Fact]
    public void A_personal_cover_is_looked_up_where_chosen_artwork_lives()
    {
        var store = new StubStore { PersonalAnswer = "personal-artwork/abc/cover.png" };
        var chosen = new string('a', 64) + ".png";

        var found = NewResolver(store, NoFrames).Find(
            Title,
            Path.Combine("C:", "anywhere", "personal-artwork", chosen));

        Assert.Equal("personal-artwork/abc/cover.png", found);
        Assert.Equal(chosen, store.LastPersonalCover);
        Assert.Equal(0, store.RemoteCalls);
    }

    /// <summary>
    /// A provider address is never mistaken for a personal cover. Until 2026-09-18 this test was
    /// called «the provider is asked first», because both lived in one field and that was the order;
    /// since LIB-021 the picked cover has its own field and wins, and what survives of the old
    /// assertion is that a provider address alone never reaches the personal store.
    /// </summary>
    [Fact]
    public void A_provider_address_alone_never_reaches_the_personal_store()
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };

        var found = NewResolver(store, NoFrames).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
        Assert.Equal(0, store.PersonalCalls);
    }

    /// <summary>ADR-0009's order: the cover somebody picked wins over the provider's.</summary>
    [Fact]
    public void The_hand_picked_cover_wins_over_the_provider_when_both_are_on_disk()
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };

        var found = NewResolver(store, NoFrames).Find(
            Title,
            "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg",
            new string('a', 64) + ".png");

        Assert.Equal("personal-artwork/abc/cover.png", found);
        Assert.Equal(0, store.RemoteCalls);
    }

    /// <summary>
    /// A picked file that is no longer on disk falls through to the provider rather than leaving an
    /// empty card: the order is a preference, not a single answer.
    /// </summary>
    [Fact]
    public void The_provider_draws_when_the_hand_picked_file_is_gone()
    {
        var store = new StubStore { RemoteAnswer = "cache/artwork/abc/poster.jpg" };

        var found = NewResolver(store, NoFrames).Find(
            Title,
            "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg",
            new string('a', 64) + ".png");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
        Assert.Equal(1, store.PersonalCalls);
    }

    /// <summary>
    /// The personal field is read with the same guard as the old shared one: anything but a cover
    /// name draws nothing and asks the store nothing, so it cannot become a reader of any file.
    /// </summary>
    [Fact]
    public void A_personal_field_that_is_not_a_cover_name_is_never_read_as_a_path()
    {
        var store = new StubStore { PersonalAnswer = "personal-artwork/abc/cover.png" };

        Assert.Null(NewResolver(store, NoFrames).Find(Title, posterPath: null, @"C:\Windows\win.ini"));
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

        Assert.Null(NewResolver(store, NoFrames).Find(Title, stored));
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

        Assert.Null(NewResolver(store, NoFrames).Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg"));
        Assert.Equal(1, store.RemoteCalls);
    }

    [Fact]
    public void A_resolver_without_a_store_is_refused_where_it_is_built()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new ResolveTitlePoster(null!, NoFrames, DefaultOrder));
        _ = Assert.Throws<ArgumentNullException>(() => new ResolveTitlePoster(new StubStore(), null!, DefaultOrder));
        _ = Assert.Throws<ArgumentNullException>(() => new ResolveTitlePoster(new StubStore(), NoFrames, null!));
    }

    /// <summary>
    /// The general setting decides when the title says nothing, so moving it in Settings moves what
    /// the whole library draws (LIB-021, ADR-0009 decision 4).
    /// </summary>
    [Fact]
    public void The_general_order_decides_when_the_title_has_none_of_its_own()
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };

        var found = new ResolveTitlePoster(store, NoFrames, new StubOrder([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame]))
            .Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg", new string('a', 64) + ".png");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
        Assert.Equal(0, store.PersonalCalls);
    }

    /// <summary>
    /// A title's own order wins over the general one, and the general one is not even asked for —
    /// which is what makes an override survive somebody moving the general order later.
    /// </summary>
    [Fact]
    public void A_titles_own_order_wins_over_the_general_one()
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };
        var general = new StubOrder([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame]);

        var found = new ResolveTitlePoster(store, NoFrames, general).Find(
            Title,
            "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg",
            new string('a', 64) + ".png",
            "Personal,Provider,Frame");

        Assert.Equal("personal-artwork/abc/cover.png", found);
        Assert.Equal(0, general.Reads);
    }

    /// <summary>
    /// An order stored for one title that no longer names origins is not obeyed and not fatal: the
    /// general one decides, which is the same repair a hand-edited settings file gets.
    /// </summary>
    [Fact]
    public void A_titles_order_that_names_nothing_valid_falls_back_to_the_general_one()
    {
        var store = new StubStore
        {
            RemoteAnswer = "cache/artwork/abc/poster.jpg",
            PersonalAnswer = "personal-artwork/abc/cover.png",
        };

        var found = new ResolveTitlePoster(store, NoFrames, new StubOrder([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame]))
            .Find(Title, "/wXsQzWtGqPMhAqYYcVOOWvpS4Vy.jpg", new string('a', 64) + ".png", "Nonsense");

        Assert.Equal("cache/artwork/abc/poster.jpg", found);
    }

    /// <summary>
    /// An order that leads with an origin this title has nothing for falls through, rather than
    /// leaving an empty card: an order is a preference and not a single answer.
    /// </summary>
    [Fact]
    public void An_order_leading_with_a_missing_origin_falls_through_to_the_next()
    {
        var store = new StubStore { PersonalAnswer = "personal-artwork/abc/cover.png" };

        var found = new ResolveTitlePoster(store, NoFrames, DefaultOrder).Find(
            Title,
            posterPath: null,
            new string('a', 64) + ".png",
            "Frame,Personal,Provider");

        Assert.Equal("personal-artwork/abc/cover.png", found);
    }

    /// <summary>
    /// A resolver whose general order is the default one, which is what every test written before
    /// the order could be changed assumed without saying so.
    /// </summary>
    private static ResolveTitlePoster NewResolver(IArtworkStore store, IAppDataPaths paths) =>
        new(store, paths, DefaultOrder);

    private sealed class StubOrder(IReadOnlyList<CoverOrigin> order) : ICoverOrderSettings
    {
        /// <summary>How many times the general order was asked for, so a test can say it was not.</summary>
        public int Reads { get; private set; }

        public IReadOnlyList<CoverOrigin> Current
        {
            get
            {
                Reads++;
                return order;
            }
        }

        public void Save(IReadOnlyList<CoverOrigin> updated) => throw new NotSupportedException();
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

        public Task RemoveTitleAsync(TitleId titleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    /// <summary>Somewhere of its own for frames, so a test never reads the real application's data.</summary>
    private sealed class FramePaths(string root) : IAppDataPaths, IDisposable
    {
        public string DataRoot { get; } = root;

        public string DatabasePath => Path.Combine(DataRoot, "library.db");

        public string SettingsPath => Path.Combine(DataRoot, "settings.json");

        public string BackupsDirectory => Path.Combine(DataRoot, "backups");

        public string PersonalArtworkDirectory => Path.Combine(DataRoot, "personal-artwork");

        public string RemoteCacheDirectory => Path.Combine(DataRoot, "cache", "artwork");

        public string CourseThumbnailDirectory => Path.Combine(DataRoot, "cache", "course-thumbnails");

        public string TitleFrameDirectory => Path.Combine(DataRoot, "cache", "title-frames");

        public string DiagnosticsDirectory => Path.Combine(DataRoot, "diagnostics");

        public string StartupRegistrySubKey => @"Software\Test";

        public string? SystemHandoffDirectory => null;

        /// <summary>A fresh root with a frame already taken for <paramref name="title"/>.</summary>
        public static FramePaths WithFrameFor(TitleId title)
        {
            var paths = new FramePaths(Path.Combine(Path.GetTempPath(), "title-frames-" + Guid.NewGuid().ToString("N")));
            var file = ResolveTitlePoster.FrameFileFor(paths, title);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, [0x89, (byte)'P', (byte)'N', (byte)'G']);
            return paths;
        }

        public void Dispose()
        {
            if (Directory.Exists(DataRoot))
            {
                Directory.Delete(DataRoot, recursive: true);
            }
        }
    }
}
