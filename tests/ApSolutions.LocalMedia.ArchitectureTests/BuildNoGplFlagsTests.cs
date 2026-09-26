// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// <c>eng/libvlc/build-nogpl.sh</c> leaves out by name what <c>--disable-gpl</c> cannot see.
/// </summary>
/// <remarks>
/// The build runs only inside VideoLAN's images, so what can be asserted here is what the script
/// hands to VideoLAN's: the two variables build.sh reads. Whether the result carries libzvbi is
/// measured on the binaries by verify-nogpl.ps1, whose canary <see cref="VerifyNoGplTeletextTests"/>
/// provokes; this catches the flag going missing before an hour of build finds it.
/// </remarks>
public sealed partial class BuildNoGplFlagsTests
{
    /// <summary>
    /// libzvbi's contrib recipe does not say REQUIRE_GPL although two of its files are GPL-2.0-only,
    /// so --disable-gpl builds it unless it is deselected by name.
    /// </summary>
    [Fact]
    public void The_contribs_leave_out_libzvbi()
    {
        Assert.Contains("--disable-zvbi", Flags("CONTRIBFLAGS"));
    }

    /// <summary>
    /// VLC's configure.sh asks for --enable-zvbi, and telx is the decoder configure builds when
    /// libzvbi is absent; it carries code converted from a GPL decoder. Both are refused by name.
    /// </summary>
    [Theory]
    [InlineData("--disable-zvbi")]
    [InlineData("--disable-telx")]
    public void VLC_is_configured_without_a_teletext_decoder(string flag)
    {
        Assert.Contains(flag, Flags("CONFIGFLAGS"));
    }

    private static string[] Flags(string variable)
    {
        var script = File.ReadAllText(RepositoryLayout.PathFromRoot("eng/libvlc/build-nogpl.sh"));
        var matches = ExportLine().Matches(script).Where(match => match.Groups[1].Value == variable).ToArray();
        Assert.True(matches.Length == 1, $"build-nogpl.sh should export {variable} exactly once, and exports it {matches.Length} times.");

        return matches[0].Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    [GeneratedRegex("(?m)^export (\\w+)=\"([^\"]*)\"$")]
    private static partial Regex ExportLine();
}
