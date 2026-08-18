// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// Whether the subtitle style a person chooses can reach the picture. It cannot, and this measures
/// why — so A11Y-002 is blocked by a number rather than by somebody having looked at a screen.
/// </summary>
/// <remarks>
/// <para>
/// The obvious test would decode one frame with the style applied and one without it and compare the
/// bitmaps. That test cannot be written honestly today: there is no way to apply the style at all, so
/// the two frames would be identical <b>by construction</b> and the comparison could not tell "not
/// implemented" from "implemented and ineffective". What can be measured is the chain, and the chain
/// is broken in two places that are each a plain fact about the tree.
/// </para>
/// <para>
/// The style itself works: <c>SubtitleStyle</c> validates, the repository stores and reads it back,
/// and <c>PreferenceResolutionPolicy</c> resolves file over series over global. Six controls change
/// it and it survives closing the window. Everything except arriving.
/// </para>
/// </remarks>
public sealed class SubtitleStyleReachTests
{
    /// <summary>
    /// Every option name LibVLC takes its subtitle drawing from. They belong to the <b>instance</b>,
    /// not to a media player, which is what makes the caching below decisive.
    /// </summary>
    private static readonly string[] SubtitleDrawingOptions =
    [
        "--freetype-fontsize",
        "--freetype-rel-fontsize",
        "--freetype-color",
        "--freetype-opacity",
        "--freetype-bold",
        "--freetype-background-color",
        "--freetype-background-opacity",
        "--freetype-outline-thickness",
        "--freetype-outline-color",
        "--freetype-shadow-opacity",
        "--freetype-font",
        "--sub-text-scale",
    ];

    /// <summary>
    /// The first half: the native instance is built with a fixed option set that configures no
    /// subtitle drawing at all, and it is created once per option set and kept for the life of the
    /// process — so nothing chosen later can change it.
    /// </summary>
    [Fact]
    public void The_native_instance_is_built_with_no_subtitle_drawing_option()
    {
        foreach (var headless in new[] { false, true })
        {
            var options = LibVlcFactory.InstanceOptions(headless);

            Assert.NotEmpty(options);
            var configuring = options
                .Where(option => SubtitleDrawingOptions.Any(name =>
                    option.StartsWith(name, StringComparison.Ordinal)))
                .ToArray();
            Assert.True(
                configuring.Length == 0,
                $"The {(headless ? "headless" : "shell")} instance is built with "
                    + $"{configuring.Length} subtitle drawing option(s): {string.Join(", ", configuring)}. "
                    + "If that ever stops being zero, this test has outlived A11Y-002's blocker and the "
                    + "matrix entry has to be revisited rather than this assertion relaxed.");
        }
    }

    /// <summary>
    /// The second half: nothing anywhere in the shipped tree ever produces one of those options, so
    /// there is no other route either — not a media option, not a player call, not a string built at
    /// runtime.
    /// </summary>
    [Fact]
    public void No_source_file_ever_names_a_subtitle_drawing_option()
    {
        var source = Directory
            .EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(source);
        var naming = source
            .Where(file => SubtitleDrawingOptions.Any(option =>
                File.ReadAllText(file).Contains(option, StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(RepositoryLayout.Root, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            naming.Length == 0,
            $"{naming.Length} source file(s) name a subtitle drawing option: {string.Join(", ", naming)}. "
                + "That would be the blocker lifting, which is good news and still means A11Y-002 has "
                + "to be re-measured rather than this test edited.");
    }

    /// <summary>
    /// And the third fact, which is why the two above cannot be worked around from the application:
    /// the engine contract has no way to be told about a style. It can switch tracks; it cannot say
    /// how one is drawn.
    /// </summary>
    [Fact]
    public void The_engine_contract_offers_no_way_to_hand_a_style_over()
    {
        // "Style" and not "Subtitle": the engine does know about subtitles — it selects a track and
        // it takes an external file — and neither of those is a say in how the text is drawn. Asking
        // the coarser question answers AddExternalSubtitleAsync and proves nothing.
        var members = typeof(IMediaPlayerEngine)
            .GetMembers()
            .Where(member => member.Name.Contains("Style", StringComparison.OrdinalIgnoreCase))
            .Select(member => member.Name)
            .ToArray();

        Assert.True(
            members.Length == 0,
            $"IMediaPlayerEngine now offers {string.Join(", ", members)}, so a style may have a route "
                + "to the picture that A11Y-002 says it does not.");

        // And the style is a real thing with real values, so what is missing is the delivery rather
        // than the subject: a test that searched for something that does not exist would pass for
        // ever and mean nothing.
        var chosen = SubtitleStyle.Create(
            fontSizePercent: 180,
            fontFamily: "Segoe UI",
            foregroundHex: "#FFFF00",
            backgroundHex: "#000000",
            backgroundOpacity: 0.8,
            outlineThickness: 3);
        Assert.NotEqual(SubtitleStyle.EngineDefault, chosen);
    }
}
