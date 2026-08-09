using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Playback;

/// <summary>
/// Opening a file that is not in the library. The session is ephemeral by construction: it carries a
/// identifier that is generated on the spot and handed to nothing that writes.
/// </summary>
public sealed class OpenLooseFileTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("reelume-loose");

    public void Dispose()
    {
        _directory.Delete(recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Every_container_the_library_recognises_can_be_opened_loose()
    {
        foreach (var extension in new[] { ".mp4", ".mkv", ".avi", ".mov", ".webm", ".m4v", ".ts", ".m2ts" })
        {
            var path = CreateFile($"sample{extension}");
            var engine = new RecordingCoordinator();
            var session = await new OpenLooseFile(engine).ExecuteAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(path, session.Path);
            Assert.Equal($"sample{extension}", session.DisplayName);
            Assert.Equal(_directory.FullName, session.FolderPath);
            Assert.NotEqual(default, session.MediaFileId);
            Assert.Single(engine.Requests);
            Assert.Equal(path, engine.Requests[0].Path);
            Assert.Equal(TimeSpan.Zero, engine.Requests[0].StartPosition);
        }
    }

    [Fact]
    public async Task Two_activations_of_the_same_file_never_reuse_an_identifier()
    {
        var path = CreateFile("sample.mkv");
        var engine = new RecordingCoordinator();
        var open = new OpenLooseFile(engine);

        var first = await open.ExecuteAsync(path, TestContext.Current.CancellationToken);
        var second = await open.ExecuteAsync(path, TestContext.Current.CancellationToken);

        Assert.NotEqual(first.MediaFileId, second.MediaFileId);
        Assert.Equal(2, engine.Requests.Count);
    }

    [Fact]
    public async Task A_file_that_is_not_there_fails_with_the_diagnosis_the_player_already_speaks()
    {
        var engine = new RecordingCoordinator();
        var missing = Path.Combine(_directory.FullName, "gone.mkv");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            new OpenLooseFile(engine).ExecuteAsync(missing, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.FileNotFound, failure.Failure.Code);
        Assert.Empty(engine.Requests);
    }

    [Fact]
    public async Task An_extension_the_library_does_not_recognise_is_refused_before_the_engine_is_asked()
    {
        var engine = new RecordingCoordinator();
        var path = CreateFile("notes.txt");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            new OpenLooseFile(engine).ExecuteAsync(path, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.UnsupportedCodec, failure.Failure.Code);
        Assert.Empty(engine.Requests);
    }

    [Fact]
    public async Task A_directory_is_not_a_media_file()
    {
        var engine = new RecordingCoordinator();

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            new OpenLooseFile(engine).ExecuteAsync(_directory.FullName, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.UnsupportedCodec, failure.Failure.Code);
        Assert.Empty(engine.Requests);
    }

    [Fact]
    public async Task Spaces_accents_and_other_alphabets_survive_the_round_trip()
    {
        foreach (var name in new[]
        {
            "una película con espacios.mkv",
            "acentuación y ñ.mp4",
            "日本語のファイル.mkv",
            "Ελληνικά.webm",
        })
        {
            var path = CreateFile(name);
            var engine = new RecordingCoordinator();

            var session = await new OpenLooseFile(engine).ExecuteAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(name, session.DisplayName);
            Assert.Equal(path, engine.Requests[0].Path);
        }
    }

    [Fact]
    public async Task A_relative_path_is_resolved_before_anything_else_looks_at_it()
    {
        var path = CreateFile("relative.mkv");
        var awkward = Path.Combine(_directory.FullName, ".", "relative.mkv");
        var engine = new RecordingCoordinator();

        var session = await new OpenLooseFile(engine).ExecuteAsync(awkward, TestContext.Current.CancellationToken);

        Assert.Equal(path, session.Path);
    }

    [Fact]
    public async Task An_empty_or_missing_path_is_refused_outright()
    {
        var open = new OpenLooseFile(new RecordingCoordinator());

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            open.ExecuteAsync("   ", TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            open.ExecuteAsync(null!, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentNullException>(() => new OpenLooseFile(null!));
    }

    [Fact]
    public async Task The_loose_session_reaches_the_engine_through_the_single_session_coordinator()
    {
        var path = CreateFile("single.mkv");
        var engine = new RecordingCoordinator();
        var open = new OpenLooseFile(engine);

        await open.ExecuteAsync(path, TestContext.Current.CancellationToken);
        await open.ExecuteAsync(path, TestContext.Current.CancellationToken);

        // The coordinator is the only thing that ever holds a session, so a second activation
        // replaces the first rather than adding one.
        Assert.Equal(1, engine.ActiveSessions);
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllBytes(path, [0, 1, 2, 3]);
        return path;
    }

    private sealed class RecordingCoordinator : IPlaybackSessionCoordinator
    {
        public List<PlaybackRequest> Requests { get; } = [];

        public int ActiveSessions { get; private set; }

        public PlaybackSession? ActiveSession { get; private set; }

        public Task<PlaybackSession> StartAsync(
            PlaybackRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            ActiveSessions = 1;
            ActiveSession = new PlaybackSession(Guid.NewGuid(), request.MediaFileId, request.Path);
            return Task.FromResult(ActiveSession);
        }

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            ActiveSessions = 0;
            ActiveSession = null;
            return Task.CompletedTask;
        }
    }
}
