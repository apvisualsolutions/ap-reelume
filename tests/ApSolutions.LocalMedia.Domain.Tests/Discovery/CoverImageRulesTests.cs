// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

/// <summary>
/// What may be accepted when somebody chooses their own cover (LIB-018).
/// </summary>
/// <remarks>
/// The lock that had to exist before the door: the import this guards read whatever file it was
/// handed, whole and with no ceiling, and wrote it into the application's own data under whatever
/// extension it arrived with — data the backup then carries. Measured on 2026-09-03, when the
/// missing door made the missing lock invisible.
/// </remarks>
public sealed class CoverImageRulesTests
{
    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    [InlineData(".JPG")]
    [InlineData(".PnG")]
    public void The_approved_images_are_approved_whatever_the_case(string extension)
    {
        Assert.True(CoverImageRules.IsApprovedExtension(extension));
    }

    /// <summary>
    /// The four absences are decisions, so they are asserted rather than left to be re-argued.
    /// </summary>
    /// <remarks>
    /// <c>.svg</c> is a document that can carry script and remote references; <c>.bmp</c> and
    /// <c>.tiff</c> are covers nobody exported first; <c>.gif</c> would animate a grid. And a video
    /// is here because it is what a person picks by mistake.
    /// </remarks>
    [Theory]
    [InlineData(".svg")]
    [InlineData(".bmp")]
    [InlineData(".tiff")]
    [InlineData(".gif")]
    [InlineData(".mkv")]
    [InlineData(".exe")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_refused(string? extension)
    {
        Assert.False(CoverImageRules.IsApprovedExtension(extension));
    }

    /// <summary>
    /// No approved image container is also an approved video container, and the reverse.
    /// </summary>
    /// <remarks>
    /// Not decoration: both lists are hand-written in the same layer, and a name landing in both
    /// would mean a video could be taken as a cover or a cover offered to the player. Asserted
    /// across the two lists rather than by re-listing either.
    /// </remarks>
    [Fact]
    public void No_extension_belongs_to_both_lists()
    {
        foreach (var image in CoverImageRules.ApprovedExtensions)
        {
            Assert.False(
                MediaFileExtensions.IsApproved(image),
                $"{image} is approved as both a cover and a video container.");
        }

        foreach (var video in MediaFileExtensions.All)
        {
            Assert.False(
                CoverImageRules.IsApprovedExtension(video),
                $"{video} is approved as both a video container and a cover.");
        }
    }

    [Fact]
    public void Nothing_chosen_says_so_rather_than_failing_some_other_way()
    {
        Assert.Equal(CoverImageVerdict.NothingChosen, CoverImageRules.Inspect(null, 100));
        Assert.Equal(CoverImageVerdict.NothingChosen, CoverImageRules.Inspect("   ", 100));
    }

    /// <summary>
    /// A file that is not an image is refused for being the wrong kind, not for its size.
    /// </summary>
    /// <remarks>
    /// The order of the two checks is the point. Somebody who picked a film by mistake gets told
    /// what kind of file this takes; telling them their film is too large would be true and useless,
    /// and it is exactly what a size check placed first would say.
    /// </remarks>
    [Fact]
    public void The_wrong_kind_is_named_before_the_wrong_size()
    {
        Assert.Equal(
            CoverImageVerdict.NotAnApprovedImage,
            CoverImageRules.Inspect(@"C:\videos\pelicula.mkv", CoverImageRules.MaximumBytes * 400));
    }

    [Fact]
    public void An_empty_file_is_refused_for_being_empty()
    {
        Assert.Equal(CoverImageVerdict.Empty, CoverImageRules.Inspect(@"C:\arte\portada.png", 0));

        // Negative arrives from a length nobody could read, and reads as empty rather than as fine.
        Assert.Equal(CoverImageVerdict.Empty, CoverImageRules.Inspect(@"C:\arte\portada.png", -1));
    }

    /// <summary>The ceiling refuses beyond itself and accepts at it.</summary>
    /// <remarks>
    /// Both sides of the boundary, because an off-by-one here refuses a cover that is exactly the
    /// size the comment promises — and the comment is what somebody would read before believing the
    /// refusal was a bug.
    /// </remarks>
    [Fact]
    public void The_ceiling_accepts_at_the_limit_and_refuses_past_it()
    {
        Assert.Equal(
            CoverImageVerdict.Approved,
            CoverImageRules.Inspect(@"C:\arte\portada.png", CoverImageRules.MaximumBytes));

        Assert.Equal(
            CoverImageVerdict.TooLarge,
            CoverImageRules.Inspect(@"C:\arte\portada.png", CoverImageRules.MaximumBytes + 1));
    }

    [Fact]
    public void An_ordinary_cover_is_approved()
    {
        Assert.Equal(
            CoverImageVerdict.Approved,
            CoverImageRules.Inspect(@"C:\arte\portada.jpg", 240_000));
    }
}
