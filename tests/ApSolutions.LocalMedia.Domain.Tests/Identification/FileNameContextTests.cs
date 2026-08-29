// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

/// <summary>
/// What the parser is handed before it reads anything: the file's own name, and the folders between
/// the library root and it. Every existing test builds this from a file nested at least one folder
/// deep, which is the ordinary shape of a library and not the only one.
/// <para>
/// MatchModels.cs stops at 9 of 10 branches and the tenth is unreachable, measured on 2026-08-30
/// rather than assumed: <c>relative is "." or ""</c> can never take the empty arm, because the only
/// path that would produce an empty relative path is a bare file name, whose directory is "" — and
/// <c>Path.GetRelativePath</c> throws on an empty path before the pattern is ever reached. Raising
/// that file above 90% branch coverage needs the source to change, not another test.
/// </para>
/// </summary>
public sealed class FileNameContextTests
{
    /// <summary>
    /// A file sitting directly in the root has no folders to read, and the parser leans on folders for
    /// the season a name does not carry. The empty list is the honest answer; the alternative is a
    /// relative path of "." offered as if it were a folder named ".", which FindContextSeason would
    /// then match against.
    /// </summary>
    [Fact]
    public void A_file_directly_in_the_root_has_no_folders_to_read()
    {
        var context = FileNameContext.ForFile(@"D:\Media\Arrival.mkv", @"D:\Media");

        Assert.Equal("Arrival.mkv", context.FileName);
        Assert.Empty(context.ParentFolders);
    }

    /// <summary>
    /// The folders arrive outermost first, which is the order a series layout is written in — show,
    /// then season — and the order FindContextSeason relies on when both a show folder and a season
    /// folder could match.
    /// </summary>
    [Fact]
    public void The_folders_between_the_root_and_the_file_arrive_outermost_first()
    {
        var context = FileNameContext.ForFile(@"D:\Media\Serie\Temporada 2\S02E05.mkv", @"D:\Media");

        Assert.Equal("S02E05.mkv", context.FileName);
        Assert.Equal(["Serie", "Temporada 2"], context.ParentFolders);
    }

    /// <summary>
    /// A drive root has no directory above it, so GetDirectoryName answers null rather than a path.
    /// Reaching GetRelativePath with that null would throw, and a scan that walked into the top of a
    /// disk would fail on the path instead of on the file.
    /// </summary>
    [Fact]
    public void A_path_with_no_directory_above_it_is_read_without_failing()
    {
        var context = FileNameContext.ForFile(@"D:\", @"D:\");

        Assert.Empty(context.ParentFolders);
    }

    [Fact]
    public void Blank_input_is_refused_rather_than_read_as_a_root()
    {
        Assert.Throws<ArgumentException>(() => FileNameContext.ForFile("  ", @"D:\Media"));
        Assert.Throws<ArgumentException>(() => FileNameContext.ForFile(@"D:\Media\a.mkv", "  "));
    }
}
