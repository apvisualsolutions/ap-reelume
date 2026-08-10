// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The full text of every third-party licence is in the repository, and every notice reproduced from
/// a package is that package's own file rather than something typed from memory.
/// </summary>
/// <remarks>
/// Naming a component and its licence is what the notices do; it is not what the licences ask for.
/// LGPL-2.1 §6, GPL-2.0 §1 and Apache-2.0 §4a each require a copy of the licence to accompany a
/// binary distribution, and MIT and BSD-3-Clause require the copyright notice to be reproduced.
/// VideoLAN's NuGet package carries no <c>COPYING</c> at all, so nothing upstream supplies it either:
/// whatever the artifact does not carry, nobody carries.
/// <para>
/// The reproduced notices are compared against the restored package byte for byte instead of being
/// eyeballed once. A notice copied by hand is right on the day it is copied and silently wrong at the
/// next version bump, which is the same failure that left the third-party notices naming eight
/// components while the package carried thirty.
/// </para>
/// </remarks>
public sealed class LicenceTextTests
{
    private const string LicenceDirectory = "docs/release/licenses";

    /// <summary>The project whose closure becomes the published payload.</summary>
    private const string PackagedProject = "src/ApSolutions.LocalMedia.Windows/packages.lock.json";

    /// <summary>
    /// The program's own licence. It travels as <c>LICENSE</c> at the root of the artifact, so it is
    /// the one identifier the notices may name without a file in this folder.
    /// </summary>
    private const string OwnLicence = "GPL-3.0-or-later";

    /// <summary>
    /// Every licence this repository knows how to file, with the last line of its canonical text.
    /// </summary>
    /// <remarks>
    /// A closed list rather than a filter, for the same reason the diagnostics allowlist is closed: a
    /// filter has to imagine every licence that could arrive, and this one only has to be told about
    /// the ones that did. <see cref="No_unknown_licence_reaches_the_artifact_unfiled"/> is what forces
    /// the list to grow.
    /// </remarks>
    private static readonly (string Identifier, string FileName, string LastLine)[] KnownLicences =
    [
        ("Apache-2.0", "Apache-2.0.txt", "limitations under the License."),
        ("BSD-3-Clause", "BSD-3-Clause.txt", "POSSIBILITY OF SUCH DAMAGE."),
        ("GPL-2.0-or-later", "GPL-2.0.txt", "Public License instead of this License."),
        ("LGPL-2.1-or-later", "LGPL-2.1.txt", "That's all there is to it!"),
        ("MIT", "MIT.txt", "IN THE SOFTWARE."),
    ];

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>Anything shaped like an SPDX identifier, so an unfiled licence cannot pass unseen.</summary>
    private static readonly Regex LicenceShaped = new(
        @"(?<![\w-])(?:MIT|(?:L?GPL|Apache|BSD|MPL|EPL|CDDL)-[0-9][0-9A-Za-z.\-]*)",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// Notices copied verbatim out of a restored package: the notice file, the package that publishes
    /// it, and the file inside that package.
    /// </summary>
    public static TheoryData<string, string, string> ReproducedNotices() =>
        new()
        {
            { "NOTICE-ANGLE.txt", "Avalonia.Angle.Windows.Natives", "LICENSE" },
            { "NOTICE-BouncyCastle.txt", "BouncyCastle.Cryptography", "LICENSE.md" },
            { "NOTICE-HarfBuzzSharp.txt", "HarfBuzzSharp", "LICENSE.txt" },
            { "NOTICE-SkiaSharp.txt", "SkiaSharp", "LICENSE.txt" },
            { "NOTICE-Skia-HarfBuzz-natives.txt", "SkiaSharp.NativeAssets.Win32", "THIRD-PARTY-NOTICES.txt" },
            { "NOTICE-SQLite.txt", "SQLitePCLRaw.lib.e_sqlite3", "LICENSE.txt" },
        };

    /// <summary>
    /// Notices this repository assembles, because the package ships no licence file at all. Each one
    /// has to reproduce the copyright its own package declares, which is the only statement of it
    /// there is: the MIT components share one file with the licence text they all point at.
    /// </summary>
    public static TheoryData<string, string> AssembledNotices() =>
        new()
        {
            { "MIT.txt", "Avalonia" },
            { "MIT.txt", "MicroCom.Runtime" },
            { "MIT.txt", "Microsoft.Data.Sqlite" },
            { "MIT.txt", "Tmds.DBus.Protocol" },
            { "NOTICE-SQLitePCLRaw.txt", "SQLitePCLRaw.core" },
        };

    [Fact]
    public void Every_licence_the_notices_declare_has_its_text_in_the_repository()
    {
        var declared = DistributedSection();
        var missing = KnownLicences
            .Where(licence => declared.Contains(licence.Identifier, StringComparison.Ordinal))
            .Where(licence => !File.Exists(LicencePath(licence.FileName)))
            .Select(licence => $"{licence.Identifier} has no {licence.FileName}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"The notices declare licences whose text the artifact would not carry: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// A truncated licence is not a copy of the licence. How it ends is checked rather than how long
    /// it is, because a file cut in half is still long. Line breaks are collapsed first: where a
    /// sentence wraps is a typesetting decision and every one of these texts wraps differently.
    /// </summary>
    [Fact]
    public void Every_licence_text_is_whole_rather_than_an_excerpt()
    {
        var truncated = KnownLicences
            .Where(licence => File.Exists(LicencePath(licence.FileName)))
            .Where(licence => !Collapsed(File.ReadAllText(LicencePath(licence.FileName)))
                .EndsWith(licence.LastLine, StringComparison.Ordinal))
            .Select(licence => $"{licence.FileName} does not end with \"{licence.LastLine}\"")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(truncated.Length == 0, string.Join("; ", truncated));
    }

    /// <summary>
    /// Every licence the notices declare for a distributed component is one this repository files
    /// somewhere. A dependency arriving under a licence nobody thought about is exactly the case a
    /// filter would wave through.
    /// </summary>
    /// <remarks>
    /// Only the table rows are read, because that is where a component's licence is declared and
    /// where <c>ThirdPartyNoticeTests</c> guarantees every distributed package has a line. The prose
    /// around them discusses licences the artifact does not carry — a <c>GPL-2.0-only</c> plugin is
    /// named there precisely as the case that would not be compatible.
    /// </remarks>
    [Fact]
    public void No_unknown_licence_reaches_the_artifact_unfiled()
    {
        var known = KnownLicences.Select(licence => licence.Identifier).Append(OwnLicence).ToArray();
        var unfiled = LicenceShaped
            .Matches(DeclarationRows())
            .Select(match => match.Value)
            .Where(identifier => !known.Contains(identifier, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unfiled.Length == 0,
            $"The notices name licences this suite does not know how to file: {string.Join(", ", unfiled)}. "
                + $"Add the text to {LicenceDirectory} and the identifier to KnownLicences.");
    }

    [Theory]
    [MemberData(nameof(ReproducedNotices))]
    public void A_reproduced_notice_is_the_package_own_file(string noticeFile, string package, string fileInPackage)
    {
        var upstream = Path.Combine(PackageRoot(package), fileInPackage);
        Assert.True(File.Exists(upstream), $"{package} does not carry {fileInPackage} at {upstream}.");

        var shipped = LicencePath(noticeFile);
        Assert.True(File.Exists(shipped), $"{noticeFile} is missing from {LicenceDirectory}.");

        Assert.Equal(Normalised(File.ReadAllText(upstream)), Normalised(File.ReadAllText(shipped)));
    }

    [Theory]
    [MemberData(nameof(AssembledNotices))]
    public void An_assembled_notice_reproduces_the_copyright_its_package_declares(string noticeFile, string package)
    {
        var declared = DeclaredCopyright(package);
        Assert.False(
            string.IsNullOrWhiteSpace(declared),
            $"{package} declares no copyright, so {noticeFile} has nothing to reproduce.");

        var shipped = LicencePath(noticeFile);
        Assert.True(File.Exists(shipped), $"{noticeFile} is missing from {LicenceDirectory}.");

        Assert.Contains(declared, File.ReadAllText(shipped), StringComparison.Ordinal);
    }

    /// <summary>
    /// LibVLC is the one component whose licence obligation reaches past a copyright line: LGPL-2.1 §6
    /// is met by naming the unmodified upstream build the binaries came from, so the notice states the
    /// resolved versions rather than the package name alone.
    /// </summary>
    [Fact]
    public void The_videolan_notice_names_the_exact_build_its_binaries_came_from()
    {
        var notice = File.ReadAllText(LicencePath("NOTICE-VideoLAN.txt"));
        var resolved = ResolvedPackages();

        foreach (var package in new[] { "VideoLAN.LibVLC.Windows", "LibVLCSharp" })
        {
            Assert.Contains($"{package} {resolved[package]}", notice, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Both packaging scripts carry the folder. One updated and the other forgotten is how the ARM64
    /// artifact would quietly ship without the texts the x64 one carries.
    /// </summary>
    [Theory]
    [InlineData("eng/package-x64.ps1")]
    [InlineData("eng/package-arm64.ps1")]
    public void The_packaging_script_carries_the_licence_folder_into_the_payload(string script)
    {
        Assert.Contains(
            LicenceDirectory,
            File.ReadAllText(RepositoryLayout.PathFromRoot(script)),
            StringComparison.Ordinal);
    }

    private static string LicencePath(string fileName) =>
        RepositoryLayout.PathFromRoot($"{LicenceDirectory}/{fileName}");

    /// <summary>
    /// The part of the notices that describes what the artifact ships. The development-only table is
    /// deliberately outside it: those packages build and test the program and never travel with it.
    /// </summary>
    private static string DistributedSection()
    {
        var notices = File.ReadAllText(
            RepositoryLayout.PathFromRoot("docs/release/THIRD-PARTY-NOTICES.en.md"));
        var start = notices.IndexOf("## Components distributed with the application", StringComparison.Ordinal);
        var end = notices.IndexOf("## Components used only during development", StringComparison.Ordinal);

        Assert.True(start >= 0 && end > start, "The notices no longer carry the section this reads.");
        return notices[start..end];
    }

    /// <summary>The table rows of that section: one component, its version and its declared licence.</summary>
    private static string DeclarationRows()
    {
        var rows = DistributedSection()
            .Split('\n')
            .Where(line => line.StartsWith('|') && !line.StartsWith("|--", StringComparison.Ordinal))
            .Skip(1)
            .ToArray();

        Assert.NotEmpty(rows);
        return string.Join('\n', rows);
    }

    /// <summary>Newline style is a checkout decision, not a licence decision.</summary>
    private static string Normalised(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    /// <summary>The same text with every run of whitespace reduced to one space.</summary>
    private static string Collapsed(string text) => Whitespace.Replace(text, " ").Trim();

    private static string DeclaredCopyright(string package)
    {
        var nuspec = Path.Combine(PackageRoot(package), $"{package.ToLowerInvariant()}.nuspec");
        Assert.True(File.Exists(nuspec), $"{package} has no nuspec at {nuspec}.");

        return XDocument.Load(nuspec)
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "copyright")?.Value.Trim()
            ?? string.Empty;
    }

    /// <summary>
    /// Where the restore left the package. Reading it is the point: this suite exists to compare what
    /// the repository claims against what the build actually consumed.
    /// </summary>
    private static string PackageRoot(string package)
    {
        var named = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        var root = string.IsNullOrWhiteSpace(named)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages")
            : named;

        Assert.True(
            Directory.Exists(root),
            $"The restored packages are not at {root}. Run `dotnet restore`, or point NUGET_PACKAGES at them.");

        var path = Path.Combine(root, package.ToLowerInvariant(), ResolvedPackages()[package]);
        Assert.True(Directory.Exists(path), $"{package} is not restored at {path}. Run `dotnet restore`.");
        return path;
    }

    private static SortedDictionary<string, string> ResolvedPackages()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(RepositoryLayout.PathFromRoot(PackagedProject)));

        var resolved = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (var package in framework.Value.EnumerateObject())
            {
                if (package.Value.GetProperty("type").GetString() != "Project")
                {
                    resolved[package.Name] = package.Value.GetProperty("resolved").GetString()!;
                }
            }
        }

        Assert.NotEmpty(resolved);
        return resolved;
    }
}
