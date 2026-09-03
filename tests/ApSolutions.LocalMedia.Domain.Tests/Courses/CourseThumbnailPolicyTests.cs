// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Courses;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Courses;

/// <summary>
/// Which frame of which lesson becomes a course's picture, and when the stored one stops being it
/// (CRS-006).
/// </summary>
/// <remarks>
/// <b>Not one of these touches a video, and that is the point of the file existing.</b> Taking a
/// frame needs LibVLC and a real file; deciding which frame, when to take it again and when to give
/// up needs neither. The tenth rule of this repository is that only the half which talks to the
/// machine is excluded from coverage, and the way a half ends up excluded that should not be is by
/// living behind the decoder rather than beside it.
/// </remarks>
public sealed class CourseThumbnailPolicyTests
{
    private static readonly DateTimeOffset Monday = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_picture_comes_from_the_first_lesson_the_catalogue_has_seen()
    {
        var lessons = new[]
        {
            Lesson(1, "La primera", hasFile: true),
            Lesson(2, "La segunda", hasFile: true),
        };

        Assert.Equal("La primera", CourseThumbnailPolicy.Source(lessons)?.Title);
    }

    /// <summary>
    /// A lesson the catalogue has never seen is skipped rather than refused: there is no file to
    /// open, and a course whose first lesson is missing still has a second one.
    /// </summary>
    [Fact]
    public void A_lesson_with_no_file_is_skipped_rather_than_refused()
    {
        var lessons = new[]
        {
            Lesson(1, "Sin archivo", hasFile: false),
            Lesson(2, "La segunda", hasFile: true),
        };

        Assert.Equal("La segunda", CourseThumbnailPolicy.Source(lessons)?.Title);
    }

    /// <summary>A course whose files the catalogue has never seen has no picture to take.</summary>
    [Fact]
    public void A_course_with_no_files_at_all_has_no_source()
    {
        var lessons = new[] { Lesson(1, "Sin archivo", hasFile: false) };

        Assert.Null(CourseThumbnailPolicy.Source(lessons));
        Assert.Null(CourseThumbnailPolicy.Source([]));
    }

    [Fact]
    public void A_null_list_is_refused_rather_than_read_as_empty()
    {
        // The two answer differently — no source against no course — and a null read as empty would
        // report «this course has nothing to show» about a call that was simply wrong.
        _ = Assert.Throws<ArgumentNullException>(() => CourseThumbnailPolicy.Source(null!));
    }

    /// <summary>The frame comes from a tenth of the way in, not from the start.</summary>
    /// <remarks>
    /// Asserted against the fraction rather than against a hand-written minute, so the two cannot
    /// drift: a test that said «two minutes and twenty-four seconds» about a 24-minute lesson would
    /// be a second copy of the constant, and copies are what disagree.
    /// </remarks>
    [Theory]
    [InlineData(24)]
    [InlineData(90)]
    [InlineData(3)]
    public void The_frame_comes_from_a_tenth_of_the_way_in(int minutes)
    {
        var duration = TimeSpan.FromMinutes(minutes);
        var at = CourseThumbnailPolicy.SeekPosition(duration);

        Assert.Equal(duration * CourseThumbnailPolicy.SeekFraction, at);

        // And it is genuinely inside the lesson: a fraction that had been left at zero, or at one,
        // would satisfy an equality written against itself.
        Assert.True(at > TimeSpan.Zero);
        Assert.True(at < duration);
    }

    /// <summary>
    /// A lesson the catalogue has no duration for seeks to the start rather than to a negative
    /// position.
    /// </summary>
    /// <remarks>
    /// What LibVLC does with a negative seek is not something this policy gets to assume, so it never
    /// asks. Both arms are here because zero and negative arrive from different places: an unscanned
    /// file and a duration that was stored wrong.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_lesson_with_no_duration_seeks_to_the_start(int minutes)
    {
        Assert.Equal(TimeSpan.Zero, CourseThumbnailPolicy.SeekPosition(TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void With_no_source_there_is_nothing_to_take()
    {
        Assert.Equal(
            CourseThumbnailAction.Impossible,
            CourseThumbnailPolicy.Decide(hasSource: false, stored: null, current: null));

        // Even with a file sitting right there: no source is about the course, not about the disk.
        Assert.Equal(
            CourseThumbnailAction.Impossible,
            CourseThumbnailPolicy.Decide(hasSource: false, stored: null, current: new(100, Monday)));
    }

    [Fact]
    public void With_no_picture_stored_one_is_taken()
    {
        Assert.Equal(
            CourseThumbnailAction.Capture,
            CourseThumbnailPolicy.Decide(hasSource: true, stored: null, current: new(100, Monday)));
    }

    /// <summary>A picture whose file still matches is kept.</summary>
    [Fact]
    public void A_picture_that_still_describes_its_file_is_kept()
    {
        var stamp = new CourseThumbnailStamp(100, Monday);

        Assert.Equal(
            CourseThumbnailAction.Keep,
            CourseThumbnailPolicy.Decide(hasSource: true, stored: stamp, current: stamp));
    }

    /// <summary>
    /// Either half of the stamp moving is enough, and both halves are asserted separately.
    /// </summary>
    /// <remarks>
    /// A re-encode at the same size and a truncation at the same second are both real, and a check
    /// that watched only one of the two would be green through the other. This is the pair the
    /// stamp exists for, so it is the pair that is measured.
    /// </remarks>
    [Fact]
    public void Either_a_new_length_or_a_new_write_time_takes_it_again()
    {
        var stored = new CourseThumbnailStamp(100, Monday);

        Assert.Equal(
            CourseThumbnailAction.Capture,
            CourseThumbnailPolicy.Decide(hasSource: true, stored: stored, current: new(101, Monday)));

        Assert.Equal(
            CourseThumbnailAction.Capture,
            CourseThumbnailPolicy.Decide(hasSource: true, stored: stored, current: new(100, Monday.AddSeconds(1))));
    }

    /// <summary>
    /// A file that is gone takes the picture again rather than declaring the course impossible.
    /// </summary>
    /// <remarks>
    /// The source lesson is chosen from the catalogue on every pass, so a file that vanished means
    /// the course now points at a different lesson — and the answer is to go and take that one, not
    /// to give up on the card.
    /// </remarks>
    [Fact]
    public void A_file_that_is_gone_is_taken_again_rather_than_given_up_on()
    {
        Assert.Equal(
            CourseThumbnailAction.Capture,
            CourseThumbnailPolicy.Decide(hasSource: true, stored: new(100, Monday), current: null));
    }

    /// <summary>The deadline separates the file that is slow from the file that has nothing.</summary>
    /// <remarks>
    /// Written as a relation to the measurement rather than as the number itself: the spike's four
    /// successes handed a frame over between 433 and 472 ms and its one failure took 4.5 s to give
    /// up, so what has to hold is that the deadline sits between them with room. A test asserting
    /// «three seconds» would pass on a deadline that had drifted to the wrong side of either.
    /// </remarks>
    [Fact]
    public void The_deadline_sits_between_the_slowest_success_and_the_failure()
    {
        var slowestSuccess = TimeSpan.FromMilliseconds(472);
        var measuredFailure = TimeSpan.FromMilliseconds(4500);

        Assert.True(
            CourseThumbnailPolicy.Deadline > slowestSuccess * 2,
            $"the deadline is {CourseThumbnailPolicy.Deadline}, which leaves no room over the slowest "
            + $"frame the spike measured ({slowestSuccess}): a slow disk would read as a broken file.");

        Assert.True(
            CourseThumbnailPolicy.Deadline < measuredFailure,
            $"the deadline is {CourseThumbnailPolicy.Deadline} and the spike's unsupported file took "
            + $"{measuredFailure} to give up, so this would wait for it rather than move on.");
    }

    private static CourseLessonProgress Lesson(int number, string title, bool hasFile) =>
        new(
            new LessonId(Guid.NewGuid()),
            hasFile ? new MediaFileId(Guid.NewGuid()) : null,
            ModuleNumber: 1,
            Module: null,
            Number: number,
            Title: title,
            Duration: TimeSpan.FromMinutes(10),
            Position: TimeSpan.Zero,
            Status: WatchStatus.NotStarted);
}
