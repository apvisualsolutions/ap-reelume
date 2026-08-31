// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Playback;

/// <summary>
/// Describing a file that is not in the library. The session is ephemeral by construction: it carries
/// an identifier generated on the spot and handed to nothing that writes.
/// </summary>
/// <remarks>
/// Nothing here asks anything of the engine, and that is the point of the class since 2026-08-17: it
/// judges whether a file may be opened and says what session it would be, and the opening itself is
/// the player's. What used to be asserted here as "refused before the engine is asked" is now true by
/// construction — there is no engine to ask — and the promise that a second activation replaces the
/// first rather than adding one moved with the opening, to the walk that presses it.
/// </remarks>
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
        // The domain's list and not a copy of it: what this asserts is «every container the library
        // recognises», so a literal here would keep passing on the day the library recognises one
        // more. It had already fallen behind by one when .flv was added.
        foreach (var extension in MediaFileExtensions.All)
        {
            var path = CreateFile($"sample{extension}");

            var session = await OpenLooseFile.ExecuteAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(path, session.Path);
            Assert.Equal($"sample{extension}", session.DisplayName);
            Assert.Equal(_directory.FullName, session.FolderPath);
            Assert.NotEqual(default, session.MediaFileId);
        }
    }

    [Fact]
    public async Task Two_activations_of_the_same_file_never_reuse_an_identifier()
    {
        var path = CreateFile("sample.mkv");

        var first = await OpenLooseFile.ExecuteAsync(path, TestContext.Current.CancellationToken);
        var second = await OpenLooseFile.ExecuteAsync(path, TestContext.Current.CancellationToken);

        Assert.NotEqual(first.MediaFileId, second.MediaFileId);
    }

    [Fact]
    public async Task A_file_that_is_not_there_fails_with_the_diagnosis_the_player_already_speaks()
    {
        var missing = Path.Combine(_directory.FullName, "gone.mkv");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            OpenLooseFile.ExecuteAsync(missing, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.FileNotFound, failure.Failure.Code);
    }

    [Fact]
    public async Task An_extension_the_library_does_not_recognise_is_refused()
    {
        var path = CreateFile("notes.txt");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            OpenLooseFile.ExecuteAsync(path, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.UnsupportedCodec, failure.Failure.Code);
    }

    [Fact]
    public async Task A_directory_is_not_a_media_file()
    {
        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            OpenLooseFile.ExecuteAsync(_directory.FullName, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.UnsupportedCodec, failure.Failure.Code);
    }

    /// <summary>
    /// The order of the two refusals, which is not interchangeable: a `.txt` that is not there is
    /// refused for being a `.txt`, because the container is decided from the name alone and saying
    /// "it is not there" about a file nobody would open anyway is the wrong diagnosis.
    /// </summary>
    [Fact]
    public async Task The_container_is_judged_before_the_file_is_looked_for()
    {
        var missing = Path.Combine(_directory.FullName, "gone.txt");

        var failure = await Assert.ThrowsAsync<PlaybackFailureException>(() =>
            OpenLooseFile.ExecuteAsync(missing, TestContext.Current.CancellationToken));

        Assert.Equal(PlaybackFailureCode.UnsupportedCodec, failure.Failure.Code);
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

            var session = await OpenLooseFile.ExecuteAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal(name, session.DisplayName);
            Assert.Equal(path, session.Path);
        }
    }

    [Fact]
    public async Task A_relative_path_is_resolved_before_anything_else_looks_at_it()
    {
        var path = CreateFile("relative.mkv");
        var awkward = Path.Combine(_directory.FullName, ".", "relative.mkv");

        var session = await OpenLooseFile.ExecuteAsync(awkward, TestContext.Current.CancellationToken);

        Assert.Equal(path, session.Path);
    }

    [Fact]
    public async Task An_empty_or_missing_path_is_refused_outright()
    {

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            OpenLooseFile.ExecuteAsync("   ", TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            OpenLooseFile.ExecuteAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_cancelled_activation_describes_nothing()
    {
        var path = CreateFile("cancelled.mkv");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OpenLooseFile.ExecuteAsync(path, cancellation.Token));
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllBytes(path, [0, 1, 2, 3]);
        return path;
    }
}
