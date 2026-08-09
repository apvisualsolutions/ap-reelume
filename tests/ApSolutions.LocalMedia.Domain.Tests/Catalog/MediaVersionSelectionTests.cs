using ApSolutions.LocalMedia.Domain.Catalog;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Catalog;

public sealed class MediaVersionSelectionTests
{
    [Fact]
    public void Available_manual_preference_wins_and_every_file_remains_visible()
    {
        var preferred = Version(1, available: true, width: 1920, height: 1080, hdr: false, codec: "H264", size: 10);
        var higherQuality = Version(2, available: true, width: 3840, height: 2160, hdr: true, codec: "HEVC", size: 20);
        var group = Group([preferred, higherQuality], preferred.MediaFileId);

        var selection = new MediaVersionSelectionPolicy().Select(group, new MediaVersionPreferences(PreferHdr: true));

        Assert.Equal(preferred.MediaFileId, selection.EffectiveVersion?.MediaFileId);
        Assert.Equal(preferred.MediaFileId, selection.StoredPreferredMediaFileId);
        Assert.Equal([preferred.MediaFileId, higherQuality.MediaFileId], selection.VisibleFileIds);
        AssertNoDestructiveSurface(selection);
    }

    [Fact]
    public void Unavailable_preference_uses_temporary_fallback_without_changing_stored_choice()
    {
        var preferred = Version(1, available: false, width: 3840, height: 2160, hdr: true, codec: "HEVC", size: 20);
        var fallback = Version(2, available: true, width: 1920, height: 1080, hdr: false, codec: "H264", size: 10);
        var policy = new MediaVersionSelectionPolicy();
        var disconnected = Group([preferred, fallback], preferred.MediaFileId);

        var whileDisconnected = policy.Select(disconnected, new MediaVersionPreferences(PreferHdr: true));
        var afterReconnect = policy.Select(
            disconnected with
            {
                Versions = disconnected.Versions
                    .Select(version => version.MediaFileId == preferred.MediaFileId ? version with { IsAvailable = true } : version)
                    .ToArray(),
            },
            new MediaVersionPreferences(PreferHdr: true));

        Assert.Equal(fallback.MediaFileId, whileDisconnected.EffectiveVersion?.MediaFileId);
        Assert.Equal(preferred.MediaFileId, whileDisconnected.StoredPreferredMediaFileId);
        Assert.Equal(preferred.MediaFileId, afterReconnect.EffectiveVersion?.MediaFileId);
        Assert.Equal(preferred.MediaFileId, afterReconnect.StoredPreferredMediaFileId);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public void Resolution_HDR_preference_codec_and_size_form_a_stable_quality_order(bool preferHdr, int expectedSeed)
    {
        var sdr = Version(1, true, 3840, 2160, false, "AV1", 30);
        var hdr = Version(2, true, 3840, 2160, true, "HEVC", 20);

        var selection = new MediaVersionSelectionPolicy().Select(
            Group([hdr, sdr], preferred: null),
            new MediaVersionPreferences(preferHdr));

        Assert.Equal(VersionId(expectedSeed), selection.EffectiveVersion?.MediaFileId);
    }

    [Fact]
    public void Exact_quality_tie_is_resolved_by_stable_file_identifier()
    {
        var later = Version(2, true, 1920, 1080, false, "H264", 10);
        var earlier = Version(1, true, 1920, 1080, false, "H264", 10);

        var selection = new MediaVersionSelectionPolicy().Select(
            Group([later, earlier], preferred: null),
            new MediaVersionPreferences(PreferHdr: true));

        Assert.Equal(earlier.MediaFileId, selection.EffectiveVersion?.MediaFileId);
    }

    [Fact]
    public void Null_or_empty_selection_input_is_rejected()
    {
        var policy = new MediaVersionSelectionPolicy();
        var preferences = new MediaVersionPreferences(PreferHdr: true);
        var empty = Group([], preferred: null);

        Assert.Throws<ArgumentNullException>(() => policy.Select(null!, preferences));
        Assert.Throws<ArgumentNullException>(() => policy.Select(empty, null!));
        Assert.Throws<ArgumentException>(() => policy.Select(empty, preferences));
    }

    private static MediaVersionGroup Group(IReadOnlyList<MediaVersion> versions, MediaFileId? preferred) => new(
        new MediaVersionId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
        "tv:show:s05e10",
        versions,
        preferred);

    private static MediaVersion Version(
        int seed,
        bool available,
        int width,
        int height,
        bool hdr,
        string codec,
        long size) => new(
            VersionId(seed),
            $"C:\\Media\\Show.5x10.{seed}.mkv",
            available,
            TimeSpan.FromMinutes(50),
            width,
            height,
            hdr,
            codec,
            size);

    private static MediaFileId VersionId(int seed)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        return new MediaFileId(new Guid(bytes));
    }

    private static void AssertNoDestructiveSurface(MediaVersionSelection selection)
    {
        var names = selection.GetType().GetMembers().Select(member => member.Name).ToArray();
        Assert.DoesNotContain(names, name => name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Hide", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }
}
