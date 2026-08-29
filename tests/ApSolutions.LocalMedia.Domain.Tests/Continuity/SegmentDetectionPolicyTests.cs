// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Continuity;

/// <summary>
/// What becomes of a detection: only valid, confident results are stored, a person's corrections
/// survive every later run, and the player never sees a detection where a manual marker exists.
/// <para>
/// MergeDetections stops at 13 of 14 branches and the fourteenth is unreachable, read off the IL on
/// 2026-08-30 rather than assumed. At offset 139 the method holds <c>dup; brtrue.s</c> — the delegate
/// cache for the GroupBy lambda — but that cache field lives on the closure class the method builds
/// to capture <c>durations</c>, and a fresh closure is allocated on every call, so the field is
/// always null and the jump is never taken. No input reaches it: the file's floor of 92 is also its
/// ceiling, and it stays in eng/coverage-debt.txt for that reason rather than for a missing test.
/// </para>
/// </summary>
public sealed class SegmentDetectionPolicyTests
{
    private static readonly SeriesId Series = new(Guid.Parse("a7c40003-0000-4000-8000-000000000001"));

    private static readonly MediaFileId FileA = new(Guid.Parse("a7c40003-0000-4000-8000-00000000000a"));

    private static readonly MediaFileId FileB = new(Guid.Parse("a7c40003-0000-4000-8000-00000000000b"));

    [Fact]
    public void A_detection_below_the_minimum_confidence_is_never_stored()
    {
        var detection = Detection(
            Segment(FileA, MarkerKind.Intro, 10, 35, SegmentDetectionPolicy.MinimumConfidence - 0.01));

        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing: []);

        Assert.Empty(merged);
    }

    [Fact]
    public void An_invalid_or_out_of_range_detection_is_never_stored()
    {
        var detection = Detection(
            Segment(FileA, MarkerKind.Intro, 35, 10, 0.9),
            Segment(FileA, MarkerKind.Credits, -5, 30, 0.9),
            Segment(FileB, MarkerKind.Intro, 10, 35, 1.2),
            Segment(FileB, MarkerKind.Credits, 10, 35, -0.1));

        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing: []);

        Assert.Empty(merged);
    }

    /// <summary>
    /// BUG-009: manual markers always validated against the episode's duration and detections were
    /// judged with <c>duration: null</c>. When the caller knows how long the file runs, a detection
    /// past its end is not a marker — it is a range no playback can ever reach.
    /// </summary>
    [Fact]
    public void A_detection_past_the_episode_end_is_not_stored_when_the_duration_is_known()
    {
        var detection = Detection(
            Segment(FileA, MarkerKind.Credits, 100, 200, 0.9),
            Segment(FileB, MarkerKind.Credits, 100, 200, 0.9));
        var durations = new Dictionary<MediaFileId, TimeSpan?>
        {
            [FileA] = TimeSpan.FromSeconds(150),
            [FileB] = null,
        };

        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing: [], durations);

        // FileA's episode ends at 150 s, so its detection is refused; FileB's duration is unknown
        // and the old judgement stands.
        Assert.Equal(FileB, Assert.Single(merged).FileId);
    }

    /// <summary>
    /// A duration table the caller only partly filled must not fail closed. Every existing test either
    /// passes no table at all or one that holds every file it detects on, so a file missing from the
    /// table had never been judged. GetValueOrDefault answers null for an absent key and null means
    /// "length unknown, let it through" — had it answered TimeSpan.Zero instead, every detection on a
    /// file whose duration the caller had not looked up would end past its episode and vanish, with
    /// no error to say so.
    /// </summary>
    [Fact]
    public void A_file_missing_from_the_duration_table_is_unknown_rather_than_zero_length()
    {
        var detection = Detection(
            Segment(FileA, MarkerKind.Credits, 100, 200, 0.9),
            Segment(FileB, MarkerKind.Credits, 100, 200, 0.9));
        var partial = new Dictionary<MediaFileId, TimeSpan?>
        {
            [FileA] = TimeSpan.FromSeconds(150),
        };

        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing: [], partial);

        Assert.Equal(FileB, Assert.Single(merged).FileId);
    }

    [Fact]
    public void A_valid_confident_detection_is_stored_with_its_version_and_without_correction()
    {
        var detection = Detection(Segment(FileA, MarkerKind.Intro, 10, 35, 0.87));

        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing: []);

        var row = Assert.Single(merged);
        Assert.Equal(Series, row.SeriesId);
        Assert.Equal(FileA, row.FileId);
        Assert.Equal(MarkerKind.Intro, row.Kind);
        Assert.Equal(TimeSpan.FromSeconds(10), row.Start);
        Assert.Equal(TimeSpan.FromSeconds(35), row.End);
        Assert.Equal(0.87, row.Confidence);
        Assert.Equal(7, row.DetectorVersion);
        Assert.False(row.UserCorrected);
        Assert.NotEqual(Guid.Empty, row.Id);
    }

    [Fact]
    public void A_re_detection_replaces_uncorrected_rows_and_never_touches_corrected_ones()
    {
        var corrected = Row(FileA, MarkerKind.Intro, 12, 37, corrected: true);
        var stale = Row(FileB, MarkerKind.Intro, 8, 30, corrected: false);
        var detection = Detection(
            Segment(FileA, MarkerKind.Intro, 20, 45, 0.95),
            Segment(FileB, MarkerKind.Intro, 9, 31, 0.9));

        var merged = SegmentDetectionPolicy.MergeDetections(detection, [corrected, stale]);

        Assert.Equal(2, merged.Count);
        Assert.Contains(corrected, merged);
        var replaced = Assert.Single(merged, row => row.FileId == FileB);
        Assert.Equal(TimeSpan.FromSeconds(9), replaced.Start);
        Assert.DoesNotContain(merged, row => row.Id == stale.Id);
    }

    [Fact]
    public void An_uncorrected_row_the_run_no_longer_finds_disappears()
    {
        var stale = Row(FileA, MarkerKind.Credits, 100, 130, corrected: false);

        var merged = SegmentDetectionPolicy.MergeDetections(Detection(), [stale]);

        Assert.Empty(merged);
    }

    [Fact]
    public void One_row_survives_per_file_and_kind_and_the_most_confident_wins()
    {
        var detection = Detection(
            Segment(FileA, MarkerKind.Intro, 10, 35, 0.7),
            Segment(FileA, MarkerKind.Intro, 11, 36, 0.9));

        var merged = SegmentDetectionPolicy.MergeDetections(detection, existing: []);

        var row = Assert.Single(merged);
        Assert.Equal(TimeSpan.FromSeconds(11), row.Start);
        Assert.Equal(0.9, row.Confidence);
    }

    [Fact]
    public void The_player_sees_manual_markers_and_only_the_detected_kinds_no_manual_marker_covers()
    {
        var manualIntro = new IntroMarker(
            Guid.NewGuid(),
            Series,
            MarkerKind.Intro,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30),
            MarkerOrigin.Manual,
            Confidence: null,
            UserCorrected: false);
        var detectedIntro = Row(FileA, MarkerKind.Intro, 12, 37, corrected: false);
        var detectedCredits = Row(FileA, MarkerKind.Credits, 100, 130, corrected: false);

        var composed = SegmentDetectionPolicy.ComposeForFile(
            [manualIntro],
            [detectedIntro, detectedCredits]);

        Assert.Equal(2, composed.Count);
        Assert.Contains(manualIntro, composed);
        var credits = Assert.Single(composed, marker => marker.Kind == MarkerKind.Credits);
        Assert.Equal(detectedCredits.Id, credits.Id);
        Assert.Equal(MarkerOrigin.Detected, credits.Origin);
        Assert.Equal(detectedCredits.Confidence, credits.Confidence);
        Assert.Equal(TimeSpan.FromSeconds(100), credits.Start);
        Assert.Equal(TimeSpan.FromSeconds(130), credits.End);
        Assert.DoesNotContain(composed, marker =>
            marker.Kind == MarkerKind.Intro && marker.Origin == MarkerOrigin.Detected);
    }

    [Fact]
    public void Accepting_a_row_locks_it_against_the_next_run()
    {
        var row = Row(FileA, MarkerKind.Intro, 10, 35, corrected: false);

        var accepted = SegmentDetectionPolicy.Accept(row);

        Assert.True(accepted.UserCorrected);
        Assert.Equal(row.Start, accepted.Start);
        Assert.Equal(row.End, accepted.End);

        var afterRun = SegmentDetectionPolicy.MergeDetections(
            Detection(Segment(FileA, MarkerKind.Intro, 50, 80, 0.99)),
            [accepted]);
        Assert.Equal([accepted], afterRun);
    }

    [Fact]
    public void Correcting_a_row_keeps_the_new_range_and_survives_the_next_run()
    {
        var row = Row(FileA, MarkerKind.Intro, 10, 35, corrected: false);

        var correctedRow = SegmentDetectionPolicy.Correct(
            row,
            TimeSpan.FromSeconds(14),
            TimeSpan.FromSeconds(39),
            duration: TimeSpan.FromMinutes(40));

        Assert.NotNull(correctedRow);
        Assert.True(correctedRow!.UserCorrected);
        Assert.Equal(TimeSpan.FromSeconds(14), correctedRow.Start);
        Assert.Equal(TimeSpan.FromSeconds(39), correctedRow.End);
    }

    [Fact]
    public void A_correction_with_an_impossible_range_is_refused()
    {
        var row = Row(FileA, MarkerKind.Intro, 10, 35, corrected: false);

        Assert.Null(SegmentDetectionPolicy.Correct(
            row,
            TimeSpan.FromSeconds(39),
            TimeSpan.FromSeconds(14),
            duration: TimeSpan.FromMinutes(40)));
        Assert.Null(SegmentDetectionPolicy.Correct(
            row,
            TimeSpan.FromSeconds(14),
            TimeSpan.FromMinutes(41),
            duration: TimeSpan.FromMinutes(40)));
    }

    private static SeriesSegmentDetection Detection(params DetectedSegment[] segments) =>
        new(Series, DetectorVersion: 7, segments);

    private static DetectedSegment Segment(
        MediaFileId file,
        MarkerKind kind,
        double startSeconds,
        double endSeconds,
        double confidence) =>
        new(file, kind, TimeSpan.FromSeconds(startSeconds), TimeSpan.FromSeconds(endSeconds), confidence);

    private static DetectedMarker Row(
        MediaFileId file,
        MarkerKind kind,
        double startSeconds,
        double endSeconds,
        bool corrected) =>
        new(
            Guid.NewGuid(),
            Series,
            file,
            kind,
            TimeSpan.FromSeconds(startSeconds),
            TimeSpan.FromSeconds(endSeconds),
            Confidence: 0.8,
            DetectorVersion: 7,
            UserCorrected: corrected);
}
