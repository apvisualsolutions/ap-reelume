// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Metadata;

/// <summary>
/// Taking a file somebody chose and making it a title's cover (LIB-018).
/// </summary>
/// <remarks>
/// The call that did not exist until 2026-09-03: the store had known how to import a personal image
/// since the artwork work landed, the backup carried what it wrote, and nothing anywhere called it.
/// <para>
/// These use real temporary files rather than a filesystem double, because what is being checked is
/// partly the file's own size and existence — a double would be asserting that this class reads a
/// number somebody handed it.
/// </para>
/// </remarks>
public sealed class SetPersonalCoverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ap-reelume-cover-" + Guid.NewGuid().ToString("N"));

    private readonly TitleId _title = new(Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task An_ordinary_image_is_imported_and_its_path_answered()
    {
        var store = new RecordingStore();
        var chosen = Write("portada.png", 2048);

        var result = await new SetPersonalCover(store).ExecuteAsync(
            _title,
            chosen,
            "El cartel",
            TestContext.Current.CancellationToken);

        Assert.Equal(CoverImageVerdict.Approved, result.Verdict);
        Assert.True(result.Succeeded);
        Assert.Equal(store.Answered, result.Path);
        Assert.Equal(chosen, store.LastSource);
        Assert.Equal("El cartel", store.LastAlternativeText);
        Assert.Equal(_title, store.LastTitle);
    }

    /// <summary>Nothing chosen is answered as such, and the store is not touched.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nothing_chosen_never_reaches_the_store(string? chosen)
    {
        var store = new RecordingStore();

        var result = await new SetPersonalCover(store).ExecuteAsync(
            _title,
            chosen,
            "algo",
            TestContext.Current.CancellationToken);

        Assert.Equal(CoverImageVerdict.NothingChosen, result.Verdict);
        Assert.False(result.Succeeded);
        Assert.Null(result.Path);
        Assert.Equal(0, store.Calls);
    }

    /// <summary>
    /// Every refusal is answered rather than thrown, and none of them reaches the store.
    /// </summary>
    /// <remarks>
    /// The three arrive from different places — the wrong kind of file, one that is too big, one
    /// that is empty — and what they share is that the file is never opened. A check that only
    /// tested the verdict would pass over an implementation that copied the file first and judged it
    /// afterwards.
    /// </remarks>
    [Fact]
    public async Task Every_refusal_is_answered_and_the_file_is_never_opened()
    {
        var store = new RecordingStore();
        var subject = new SetPersonalCover(store);

        var notAnImage = await subject.ExecuteAsync(
            _title, Write("pelicula.mkv", 2048), "x", TestContext.Current.CancellationToken);
        Assert.Equal(CoverImageVerdict.NotAnApprovedImage, notAnImage.Verdict);

        var empty = await subject.ExecuteAsync(
            _title, Write("vacia.png", 0), "x", TestContext.Current.CancellationToken);
        Assert.Equal(CoverImageVerdict.Empty, empty.Verdict);

        var tooLarge = await subject.ExecuteAsync(
            _title,
            Write("enorme.png", (int)CoverImageRules.MaximumBytes + 1),
            "x",
            TestContext.Current.CancellationToken);
        Assert.Equal(CoverImageVerdict.TooLarge, tooLarge.Verdict);

        Assert.Equal(0, store.Calls);
    }

    /// <summary>A path that points at nothing is refused for being empty rather than opened.</summary>
    /// <remarks>
    /// Somebody can type a path into the field, and a file that is not there has no length to read.
    /// Zero is what the inspection is handed, and «empty» is the honest answer to it.
    /// </remarks>
    [Fact]
    public async Task A_file_that_is_not_there_is_refused()
    {
        var store = new RecordingStore();

        var result = await new SetPersonalCover(store).ExecuteAsync(
            _title,
            Path.Combine(_root, "nunca-existio.png"),
            "x",
            TestContext.Current.CancellationToken);

        Assert.Equal(CoverImageVerdict.Empty, result.Verdict);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public void A_store_is_required()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new SetPersonalCover(null!));
    }

    /// <summary>The reference the store answers with carries where it came from and whether it travels.</summary>
    /// <remarks>
    /// Asserted because the backup reads exactly these two: a personal cover is somebody's own, so it
    /// is exportable, and a downloaded one is not. Nothing else in this suite reaches the record.
    /// </remarks>
    [Fact]
    public void The_reference_says_where_it_came_from_and_whether_it_travels()
    {
        var personal = new ArtworkReference(@"C:\datos\1.png", ArtworkOrigin.Personal, "El cartel", IsExportable: true);
        var remote = personal with { Origin = ArtworkOrigin.RemoteCache, IsExportable = false };

        Assert.Equal(ArtworkOrigin.Personal, personal.Origin);
        Assert.True(personal.IsExportable);
        Assert.Equal("El cartel", personal.AlternativeText);
        Assert.Equal(@"C:\datos\1.png", personal.Path);

        Assert.NotEqual(personal, remote);
        Assert.False(remote.IsExportable);
    }

    private string Write(string name, int bytes)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private sealed class RecordingStore : IArtworkStore
    {
        public int Calls { get; private set; }

        public string Answered { get; } = Path.Combine("personal-artwork", "abc", "1.png");

        public TitleId LastTitle { get; private set; }

        public string? LastSource { get; private set; }

        public string? LastAlternativeText { get; private set; }

        public string? Find(TitleId titleId, Uri source) => null;


        public string? FindPersonal(TitleId titleId, string coverFileName) => null;

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
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastTitle = titleId;
            LastSource = sourcePath;
            LastAlternativeText = alternativeText;
            return Task.FromResult(new ArtworkReference(Answered, ArtworkOrigin.Personal, alternativeText, IsExportable: true));
        }
    }
}
