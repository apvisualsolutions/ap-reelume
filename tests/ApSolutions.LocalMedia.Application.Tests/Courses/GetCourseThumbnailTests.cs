// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Courses;

/// <summary>
/// A course's picture (CRS-006): when it is taken, when it is kept, and when nothing is decoded.
/// </summary>
/// <remarks>
/// <b>Not one of these opens a video.</b> The grabber is a double that records what it was asked
/// for, which is the whole point of the port existing: the decision of which frame and whether one
/// is needed is arithmetic, and arithmetic behind a decoder is arithmetic nobody can test.
/// </remarks>
public sealed class GetCourseThumbnailTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ap-reelume-thumb-" + Guid.NewGuid().ToString("N"));

    private readonly CourseId _course = new(Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>A cover somebody chose wins, and nothing is decoded at all.</summary>
    /// <remarks>
    /// The order matters as much as the answer: a picture the application took for itself must never
    /// sit over one a person picked, and the cheapest frame is the one nobody takes.
    /// </remarks>
    [Fact]
    public async Task A_chosen_cover_wins_and_nothing_is_decoded()
    {
        var chosen = WriteFile("chosen.png", "an image");
        var grabber = new RecordingGrabber();
        var subject = Subject(grabber);

        var answer = await subject.ExecuteAsync(
            _course,
            [Lesson(hasFile: true)],
            _ => WriteFile("lesson.mkv", "a video"),
            chosenCover: chosen,
            TestContext.Current.CancellationToken);

        Assert.Equal(chosen, answer);
        Assert.Equal(0, grabber.Calls);
    }

    /// <summary>
    /// A chosen cover whose file has gone is not used, and the course falls back to its own frame.
    /// </summary>
    /// <remarks>
    /// The stored path outlives the file: somebody can delete or move the image they picked, and a
    /// card pointing at a file that is not there would draw nothing while the course had a perfectly
    /// good lesson to take a picture from.
    /// </remarks>
    [Fact]
    public async Task A_chosen_cover_that_is_gone_falls_back_to_the_video()
    {
        var grabber = new RecordingGrabber { Succeeds = true };
        var subject = Subject(grabber);

        var answer = await subject.ExecuteAsync(
            _course,
            [Lesson(hasFile: true)],
            _ => WriteFile("lesson.mkv", "a video"),
            chosenCover: Path.Combine(_root, "deleted.png"),
            TestContext.Current.CancellationToken);

        Assert.Equal(subject.FileFor(_course), answer);
        Assert.Equal(1, grabber.Calls);
    }

    [Fact]
    public async Task A_course_whose_files_the_catalogue_has_not_seen_gets_no_picture()
    {
        var grabber = new RecordingGrabber();
        var subject = Subject(grabber);

        var answer = await subject.ExecuteAsync(
            _course,
            [Lesson(hasFile: false)],
            _ => null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(answer);
        Assert.Equal(0, grabber.Calls);
    }

    /// <summary>The frame is asked for at a tenth of the lesson, and written where it belongs.</summary>
    [Fact]
    public async Task The_frame_is_asked_for_a_tenth_of_the_way_into_the_first_lesson()
    {
        var video = WriteFile("lesson.mkv", "a video");
        var grabber = new RecordingGrabber { Succeeds = true };
        var subject = Subject(grabber);

        var answer = await subject.ExecuteAsync(
            _course,
            [Lesson(hasFile: true, duration: TimeSpan.FromMinutes(40))],
            _ => video,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(subject.FileFor(_course), answer);
        Assert.Equal(video, grabber.LastVideo);
        Assert.Equal(TimeSpan.FromMinutes(4), grabber.LastAt);
        Assert.Equal(subject.FileFor(_course), grabber.LastDestination);
    }

    /// <summary>A decoder that cannot answer leaves the card without a picture, not with an error.</summary>
    [Fact]
    public async Task A_file_no_decoder_understands_leaves_no_picture()
    {
        var grabber = new RecordingGrabber { Succeeds = false };
        var subject = Subject(grabber);

        var answer = await subject.ExecuteAsync(
            _course,
            [Lesson(hasFile: true)],
            _ => WriteFile("lesson.mkv", "a video"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(answer);
        Assert.Equal(1, grabber.Calls);
    }

    /// <summary>
    /// A second pass over an unchanged course decodes nothing, and a changed file is taken again.
    /// </summary>
    /// <remarks>
    /// <b>Both halves in one test, because the pair is what matters.</b> A cache that never reused
    /// anything would pass a test about freshness, and one that never refreshed would pass a test
    /// about reuse; what has to hold is that it does each in its own case.
    /// </remarks>
    [Fact]
    public async Task An_unchanged_course_is_not_decoded_twice_and_a_changed_file_is()
    {
        var video = WriteFile("lesson.mkv", "a video");
        var grabber = new RecordingGrabber { Succeeds = true };
        var subject = Subject(grabber);
        var lessons = new[] { Lesson(hasFile: true) };

        // The grabber writes nothing, so the picture has to exist for the second pass to reuse it.
        grabber.OnCapture = destination => File.WriteAllText(destination, "a picture");

        _ = await subject.ExecuteAsync(_course, lessons, _ => video, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, grabber.Calls);

        _ = await subject.ExecuteAsync(_course, lessons, _ => video, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(1, grabber.Calls);

        // Re-encoded at a different size: the same lesson, a different file.
        File.WriteAllText(video, "a longer video than before");

        _ = await subject.ExecuteAsync(_course, lessons, _ => video, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, grabber.Calls);
    }

    /// <summary>
    /// A lesson whose file is not on the disk right now keeps the picture already taken.
    /// </summary>
    /// <remarks>
    /// A removable drive rather than a defect. Throwing the picture away would mean a library on an
    /// external disk lost every course card the moment it was unplugged, and got them back only by
    /// decoding everything again.
    /// </remarks>
    [Fact]
    public async Task A_lesson_on_a_disconnected_disk_keeps_the_picture_already_taken()
    {
        var grabber = new RecordingGrabber { Succeeds = true };
        var subject = Subject(grabber);
        Directory.CreateDirectory(Path.Combine(_root, "cache", "course-thumbnails"));
        File.WriteAllText(subject.FileFor(_course), "a picture taken earlier");

        var answer = await subject.ExecuteAsync(
            _course,
            [Lesson(hasFile: true)],
            _ => Path.Combine(_root, "unplugged", "lesson.mkv"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(subject.FileFor(_course), answer);
        Assert.Equal(0, grabber.Calls);
    }

    [Fact]
    public async Task The_arguments_it_cannot_work_without_are_refused()
    {
        var subject = Subject(new RecordingGrabber());

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            subject.ExecuteAsync(_course, null!, _ => null, cancellationToken: TestContext.Current.CancellationToken));

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            subject.ExecuteAsync(_course, [], null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    private GetCourseThumbnail Subject(ICourseFrameGrabber grabber) =>
        new(new Paths(_root), grabber);

    private string WriteFile(string name, string content)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static CourseLessonProgress Lesson(bool hasFile, TimeSpan? duration = null) =>
        new(
            new LessonId(Guid.NewGuid()),
            hasFile ? new MediaFileId(Guid.NewGuid()) : null,
            ModuleNumber: 1,
            Module: null,
            Number: 1,
            Title: "La primera",
            Duration: duration ?? TimeSpan.FromMinutes(10),
            Position: TimeSpan.Zero,
            Status: WatchStatus.NotStarted);

    private sealed class RecordingGrabber : ICourseFrameGrabber
    {
        public int Calls { get; private set; }

        public bool Succeeds { get; init; }

        public string? LastVideo { get; private set; }

        public TimeSpan LastAt { get; private set; }

        public string? LastDestination { get; private set; }

        public Action<string>? OnCapture { get; set; }

        public Task<bool> TryCaptureAsync(
            string videoPath,
            TimeSpan at,
            string destinationPath,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastVideo = videoPath;
            LastAt = at;
            LastDestination = destinationPath;
            if (Succeeds)
            {
                OnCapture?.Invoke(destinationPath);
            }

            return Task.FromResult(Succeeds);
        }
    }

    /// <summary>Somewhere of its own, so a test never writes into the real application's data.</summary>
    private sealed class Paths(string root) : IAppDataPaths
    {
        public string DataRoot { get; } = root;

        public string DatabasePath { get; } = Path.Combine(root, "library.db");

        public string SettingsPath { get; } = Path.Combine(root, "settings.json");

        public string BackupsDirectory { get; } = Path.Combine(root, "backups");

        public string PersonalArtworkDirectory { get; } = Path.Combine(root, "personal-artwork");

        public string RemoteCacheDirectory { get; } = Path.Combine(root, "cache", "artwork");

        public string CourseThumbnailDirectory { get; } = Path.Combine(root, "cache", "course-thumbnails");

        public string DiagnosticsDirectory { get; } = Path.Combine(root, "diagnostics");

        public string StartupRegistrySubKey { get; } = @"Software\ApReelumeTests\Run";

        public string? SystemHandoffDirectory => null;
    }
}
