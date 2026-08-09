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

    [Fact]
    public void Blank_input_is_refused_rather_than_treated_as_a_root()
    {
        Assert.Throws<ArgumentException>(() => RootRemapPolicy.Resolve(["  "], [], _ => true));
        Assert.Throws<ArgumentException>(() => new RootRemap("D:\\media", "  ").Normalized());
        Assert.Throws<ArgumentNullException>(() => RootRemapPolicy.Resolve(null!, [], _ => true));
        Assert.Throws<ArgumentNullException>(() => RootRemapPolicy.Rewrite("D:\\media\\a.mkv", null!));
    }
}
