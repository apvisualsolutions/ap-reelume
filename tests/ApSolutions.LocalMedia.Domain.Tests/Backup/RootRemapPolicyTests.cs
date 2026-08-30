// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Backup;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Backup;

/// <summary>
/// Where a restored library thinks its files are. Three cases matter and the third is the dangerous one:
/// the same path twice, which would silently merge two libraries into one.
/// </summary>
public sealed class RootRemapPolicyTests
{
    [Fact]
    public void A_root_that_is_still_there_needs_no_decision()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [],
            path => path == "D:\\media");

        var decision = Assert.Single(decisions);
        Assert.Equal("D:\\media", decision.OldPath);
        Assert.Equal("D:\\media", decision.NewPath);
        Assert.Equal(RootRemapStatus.Unchanged, decision.Status);
        Assert.False(decision.IsBlocking);
    }

    [Fact]
    public void A_root_mapped_to_itself_is_unchanged_however_it_is_written()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("d:/media\\", "D:\\MEDIA")],
            _ => true);

        Assert.Equal(RootRemapStatus.Unchanged, Assert.Single(decisions).Status);
    }

    [Fact]
    public void A_root_mapped_somewhere_new_is_remapped()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("D:\\media", "F:\\library")],
            _ => true);

        var decision = Assert.Single(decisions);
        Assert.Equal("F:\\library", decision.NewPath);
        Assert.Equal(RootRemapStatus.Remapped, decision.Status);
    }

    [Fact]
    public void A_root_that_is_gone_and_unmapped_is_reported_as_missing_rather_than_guessed()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media", "E:\\archive"],
            [],
            path => path == "D:\\media");

        Assert.Equal(RootRemapStatus.Unchanged, decisions[0].Status);
        Assert.Equal(RootRemapStatus.Missing, decisions[1].Status);
        Assert.False(decisions[1].IsBlocking);
    }

    [Fact]
    public void Two_roots_pointed_at_one_folder_both_block_the_restore()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media", "E:\\archive"],
            [new RootRemap("D:\\media", "F:\\library"), new RootRemap("E:\\archive", "F:\\library\\")],
            _ => true);

        Assert.Equal(2, decisions.Count);
        Assert.All(decisions, decision => Assert.Equal(RootRemapStatus.Conflict, decision.Status));
        Assert.All(decisions, decision => Assert.True(decision.IsBlocking));
    }

    [Fact]
    public void A_conflict_needs_two_different_roots_not_the_same_one_twice()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("D:\\media", "F:\\library"), new RootRemap("d:\\MEDIA", "F:\\library")],
            _ => true);

        Assert.Equal(RootRemapStatus.Remapped, Assert.Single(decisions).Status);
    }

    [Fact]
    public void A_remap_for_a_root_the_backup_never_had_is_refused()
    {
        var failure = Assert.Throws<ArgumentException>(() => RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("Z:\\somewhere", "F:\\library")],
            _ => true));

        Assert.Contains("Z:\\somewhere", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rewriting_moves_a_file_under_its_new_root_and_keeps_the_rest_of_the_path()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("D:\\media", "F:\\library")],
            _ => true);

        Assert.Equal(
            "F:\\library\\shows\\season 1\\episode.mkv",
            RootRemapPolicy.Rewrite("D:\\media\\shows\\season 1\\episode.mkv", decisions));
    }

    [Fact]
    public void Rewriting_leaves_alone_what_no_decision_covers()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("D:\\media", "F:\\library")],
            _ => true);

        Assert.Equal(
            "G:\\elsewhere\\film.mkv",
            RootRemapPolicy.Rewrite("G:\\elsewhere\\film.mkv", decisions));
        Assert.Equal(
            "D:\\mediaserver\\film.mkv",
            RootRemapPolicy.Rewrite("D:\\mediaserver\\film.mkv", decisions));
    }

    [Fact]
    public void Rewriting_a_root_onto_itself_changes_nothing()
    {
        var decisions = RootRemapPolicy.Resolve(["D:\\media"], [], _ => true);

        Assert.Equal("D:\\media\\film.mkv", RootRemapPolicy.Rewrite("D:\\media\\film.mkv", decisions));
    }

    [Fact]
    public void The_longest_matching_root_wins_so_nested_roots_do_not_cross()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media", "D:\\media\\shows"],
            [new RootRemap("D:\\media", "F:\\a"), new RootRemap("D:\\media\\shows", "G:\\b")],
            _ => true);

        Assert.Equal("G:\\b\\episode.mkv", RootRemapPolicy.Rewrite("D:\\media\\shows\\episode.mkv", decisions));
        Assert.Equal("F:\\a\\film.mkv", RootRemapPolicy.Rewrite("D:\\media\\film.mkv", decisions));
    }

    /// <summary>
    /// The conflict is marked per root, not per batch. Two_roots_pointed_at_one_folder proves both
    /// contested roots are stopped, but every root in that restore was contested, so the arm that
    /// hands an uncontested decision back untouched had never run. A restore that blocked roots
    /// nobody aimed at the same place would be refusing work it could do, and the person would have
    /// no way to tell which pair actually collided.
    /// </summary>
    [Fact]
    public void A_conflict_between_two_roots_leaves_a_third_one_alone()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media", "E:\\archive", "H:\\extra"],
            [
                new RootRemap("D:\\media", "F:\\library"),
                new RootRemap("E:\\archive", "F:\\library"),
                new RootRemap("H:\\extra", "F:\\other"),
            ],
            _ => true);

        Assert.Equal(3, decisions.Count);
        Assert.Equal(2, decisions.Count(decision => decision.Status == RootRemapStatus.Conflict));
        var untouched = Assert.Single(decisions, decision => decision.OldPath == "H:\\extra");
        Assert.Equal(RootRemapStatus.Remapped, untouched.Status);
        Assert.False(untouched.IsBlocking);
    }

    /// <summary>
    /// The length test in Normalize is what keeps a drive root a drive root. Every other path loses a
    /// trailing separator so one folder cannot become two, but "D:\" without its separator is "D:",
    /// which on Windows names the current directory of that drive rather than its root.
    /// <para>
    /// This test stops at Normalize and at the decision. Rewrite is where the separator that makes
    /// a root a root used to be dropped, and the two tests below carry it the rest of the way.
    /// </para>
    /// </summary>
    [Fact]
    public void A_drive_root_keeps_the_separator_that_makes_it_a_root()
    {
        Assert.Equal("D:\\", RootRemapPolicy.Normalize("D:/"));
        Assert.Equal("D:\\", RootRemapPolicy.Normalize("  D:\\  "));
        Assert.Equal("D:\\media", RootRemapPolicy.Normalize("D:\\media\\"));

        var decisions = RootRemapPolicy.Resolve(
            ["D:\\"],
            [new RootRemap("D:/", "F:\\library")],
            _ => true);

        var decision = Assert.Single(decisions);
        Assert.Equal("D:\\", decision.OldPath);
        Assert.Equal("F:\\library", decision.NewPath);
    }

    /// <summary>
    /// The defect the test above named and left standing: a library stored at the top of a disk was
    /// resolved as Remapped and then rewritten to nothing at all, with no error to say so. A restore
    /// that reports success and leaves every path pointing at a drive the person just told it to
    /// stop using is worse than one that refuses, because nothing announces it.
    /// </summary>
    [Fact]
    public void A_library_at_the_top_of_a_disk_is_rewritten_like_any_other()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\"],
            [new RootRemap("D:/", "F:\\library")],
            _ => true);

        Assert.Equal(RootRemapStatus.Remapped, Assert.Single(decisions).Status);
        Assert.Equal(
            "F:\\library\\shows\\episode.mkv",
            RootRemapPolicy.Rewrite("D:\\shows\\episode.mkv", decisions));
    }

    /// <summary>
    /// The same seam from the other side. A drive root as the destination would have concatenated
    /// its separator with the one the suffix already carries, so every restored path would have held
    /// a doubled separator that no later step removes.
    /// </summary>
    [Fact]
    public void A_library_moved_to_the_top_of_a_disk_keeps_one_separator()
    {
        var decisions = RootRemapPolicy.Resolve(
            ["D:\\media"],
            [new RootRemap("D:\\media", "F:/")],
            _ => true);

        Assert.Equal(
            "F:\\shows\\episode.mkv",
            RootRemapPolicy.Rewrite("D:\\media\\shows\\episode.mkv", decisions));
    }

    [Fact]
    public void Blank_input_is_refused_rather_than_treated_as_a_root()
    {
        Assert.Throws<ArgumentException>(() => RootRemapPolicy.Resolve(["  "], [], _ => true));
        Assert.Throws<ArgumentException>(() => new RootRemap("D:\\media", "  ").Normalized());
        Assert.Throws<ArgumentNullException>(() => RootRemapPolicy.Resolve(null!, [], _ => true));
        Assert.Throws<ArgumentNullException>(() => RootRemapPolicy.Rewrite("D:\\media\\a.mkv", null!));
    }
}
