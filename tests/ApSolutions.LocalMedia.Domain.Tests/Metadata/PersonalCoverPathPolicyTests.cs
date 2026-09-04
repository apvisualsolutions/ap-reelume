// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

/// <summary>
/// The poster field is free text, so what is taken out of it is a name and never a place.
/// </summary>
/// <remarks>
/// The counterpart of <c>PosterAddressPolicyTests</c>, on the other thing that one field holds. What
/// these refuse is not a list of dangerous shapes — it is everything that is not the one shape the
/// store writes, which is why a shape nobody thought of is refused too.
/// </remarks>
public sealed class PersonalCoverPathPolicyTests
{
    /// <summary>Sixty-four lower-case hexadecimal characters: the shape of a SHA-256.</summary>
    private const string Hash =
        "9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8";

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    public void A_name_the_store_wrote_is_kept_for_every_approved_container(string extension)
    {
        var stored = $@"C:\Users\alguien\AppData\Local\ApReelume\personal-artwork\3f2b\{Hash}{extension}";

        Assert.Equal($"{Hash}{extension}", PersonalCoverPathPolicy.TryGetCoverFileName(stored));
    }

    /// <summary>
    /// Every container the import will copy is one this can read back. Two lists for one decision is
    /// how a cover becomes importable and undrawable at the same time, which is the defect this
    /// whole change exists to close.
    /// </summary>
    [Fact]
    public void Every_extension_the_import_approves_is_one_a_stored_cover_can_carry()
    {
        foreach (var extension in CoverImageRules.ApprovedExtensions)
        {
            Assert.NotNull(PersonalCoverPathPolicy.TryGetCoverFileName($"{Hash}{extension}"));
        }
    }

    /// <summary>
    /// The import writes the extension back exactly as the chosen file had it, so a person who
    /// picked «foto.PNG» has a cover whose name carries upper case. Refusing it here would make
    /// their cover undrawable for the sake of a tidiness nothing needs.
    /// </summary>
    [Theory]
    [InlineData(".PNG")]
    [InlineData(".JPG")]
    [InlineData(".WebP")]
    public void The_extension_is_read_the_way_the_approved_list_reads_it(string extension) =>
        Assert.Equal($"{Hash}{extension}", PersonalCoverPathPolicy.TryGetCoverFileName($"{Hash}{extension}"));

    /// <summary>
    /// The directory is read only to be dropped, and this is the case that matters: a backup made on
    /// one machine restores its images under the new machine's own folder while the stored value
    /// still names the old one. Keeping the name is what makes the cover appear anyway.
    /// </summary>
    [Theory]
    // Windows, which is what this application writes.
    [InlineData(@"C:\Users\alguien\AppData\Local\ApReelume\personal-artwork\3f2b\")]
    // Forward slashes, because a value can arrive from a machine whose separator is not this one's.
    [InlineData("C:/Users/otro/AppData/Local/ApReelume/personal-artwork/3f2b/")]
    [InlineData("/home/alguien/.local/share/ApReelume/personal-artwork/3f2b/")]
    // Mixed, which is legal on Windows and is what a hand-edited value looks like.
    [InlineData(@"C:\Users\alguien/personal-artwork\3f2b/")]
    // No directory at all.
    [InlineData("")]
    public void The_folder_is_dropped_wherever_the_value_was_written(string directory) =>
        Assert.Equal($"{Hash}.png", PersonalCoverPathPolicy.TryGetCoverFileName($"{directory}{Hash}.png"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_stored_is_no_cover(string? posterPath) =>
        Assert.Null(PersonalCoverPathPolicy.TryGetCoverFileName(posterPath));

    /// <summary>
    /// What a provider sends must never be read as a personal cover: the two live in one field and
    /// the only thing keeping them apart is that neither policy accepts the other's shape.
    /// </summary>
    [Theory]
    [InlineData("/wXsQvli6tWqja51pYxXNG1LFIGV.jpg")]
    [InlineData("/8uO0gUM8aNqYLs1OsTBQiXu0fEv.jpg")]
    public void A_provider_path_is_not_a_personal_cover(string posterPath)
    {
        Assert.NotNull(PosterAddressPolicy.TryBuildPosterAddress(posterPath));
        Assert.Null(PersonalCoverPathPolicy.TryGetCoverFileName(posterPath));
    }

    [Theory]
    // One character short and one character long: the length is exact, not a minimum.
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e.png")]
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e88.png")]
    // Upper-case hexadecimal, which the store never writes.
    [InlineData("9F2C4A1B8E7D6053F4A2B9C8D7E6F5A4B3C2D1E0F9A8B7C6D5E4F3A2B1C0D9E8.png")]
    // Outside the hexadecimal alphabet: 'g' and 'z' are letters and neither is a digit here.
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9eg.png")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz.png")]
    // Below the digits, which every other refusal here reaches from above. A hyphen and a plus are
    // ordinary characters in an ordinary file name, so this is the shape a name that was never a
    // hash actually has — «mi-portada» rather than something hostile.
    [InlineData("-f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.png")]
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e+.png")]
    // A digit Unicode knows that this alphabet does not.
    [InlineData("٣f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.png")]
    // The right length and no extension at all, and the right length with nothing after the dot.
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8")]
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.")]
    // A separator where the dot has to be.
    [InlineData(@"9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8\png")]
    public void A_name_that_is_not_the_one_the_store_writes_is_refused(string posterPath) =>
        Assert.Null(PersonalCoverPathPolicy.TryGetCoverFileName(posterPath));

    /// <summary>
    /// A second dot leaves a remainder the approved list does not contain, so «.png.exe» and
    /// «.png:oculto» are refused by the comparison that already exists rather than by a rule each.
    /// </summary>
    [Theory]
    [InlineData(".png.exe")]
    [InlineData(".png:oculto")]
    [InlineData(".jpg.lnk")]
    // Containers the import refuses for reasons of its own, refused here for the same reason.
    [InlineData(".svg")]
    [InlineData(".bmp")]
    [InlineData(".tiff")]
    [InlineData(".gif")]
    [InlineData(".exe")]
    [InlineData(".mkv")]
    public void Only_an_approved_container_follows_the_name(string extension) =>
        Assert.Null(PersonalCoverPathPolicy.TryGetCoverFileName($"{Hash}{extension}"));

    /// <summary>
    /// The claim the whole design rests on, asserted rather than argued: whatever hostile thing the
    /// field holds, what comes back either is nothing or cannot name a place. A name of this
    /// alphabet plus an approved container has no separator to walk with, no colon to change drive
    /// or open a stream, and no pair of dots to climb.
    /// </summary>
    [Theory]
    [InlineData(@"..\..\..\Windows\System32\config\SAM")]
    [InlineData("../../../etc/passwd")]
    [InlineData(@"\\servidor\recurso\secreto.png")]
    [InlineData(@"\\?\C:\Windows\win.ini")]
    [InlineData(@"\\.\CON")]
    [InlineData("CON")]
    [InlineData("NUL.png")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("https://evil.example/x.png")]
    [InlineData(@"C:\ruta\normal\foto.png")]
    [InlineData(@"9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.png\..\..\otro.png")]
    [InlineData("9f2c4a1b8e7d6053f4a2b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8.png:zone.identifier")]
    public void A_hostile_value_can_never_name_a_place(string posterPath)
    {
        var name = PersonalCoverPathPolicy.TryGetCoverFileName(posterPath);
        if (name is null)
        {
            return;
        }

        // Reached only if some future edit widens the alphabet: the assertion is here so that the
        // widening fails loudly instead of quietly composing a path out of somebody else's text.
        Assert.DoesNotContain('\\', name);
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
        Assert.Equal(1, name.Count(character => character == '.'));
        Assert.Equal(PersonalCoverPathPolicy.NameLength, name.IndexOf('.', StringComparison.Ordinal));
    }

    /// <summary>
    /// The refusals above are what this returns; the length is what the store's own name is measured
    /// against, and it is read from the policy rather than written a second time.
    /// </summary>
    [Fact]
    public void The_agreed_length_is_the_length_of_a_hash() =>
        Assert.Equal(PersonalCoverPathPolicy.NameLength, Hash.Length);
}
