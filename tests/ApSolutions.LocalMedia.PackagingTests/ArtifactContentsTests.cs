// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// What is actually inside the thing people download. The manifest describes the package; this suite
/// opens it.
/// </summary>
/// <remarks>
/// Two of these checks exist because the default publish gets them wrong. A self-contained win-x64
/// publish of this application ships LibVLC for three architectures, so "x64 build" would otherwise
/// mean "x64 build plus two thirds of dead weight the loader will never open"; and a repository that
/// is about to be public cannot ship a binary carrying the absolute paths of the machine that built
/// it.
/// </remarks>
public sealed class ArtifactContentsTests
{
    private const ushort Amd64 = PortableExecutableHeader.Amd64;

    /// <summary>Extensions whose contents can be read as text without pulling half a gigabyte into memory.</summary>
    private static readonly string[] TextLike =
        [".json", ".txt", ".md", ".xml", ".config", ".appxmanifest", ".ps1", ".runtimeconfig.json"];

    /// <summary>
    /// Anything that names the machine that produced the artifact. The repository is going public and
    /// the package travels further than the repository does.
    /// </summary>
    private static readonly string[] PrivacyMarkers =
        [@"C:\Users\", "C:/Users/", @"\Users\", "AP_LOCALMEDIA_TMDB_TOKEN="];

    [Fact]
    public void Both_distribution_paths_exist_and_hash_to_what_the_release_publishes()
    {
        var contents = PackageEvidence.ReadReport("contents.json");
        var root = PackageEvidence.PackageRoot();
        var sums = ReadChecksums(Path.Combine(root, "SHA256SUMS.txt"));

        foreach (var name in new[] { contents.RequiredString("msix"), contents.RequiredString("zip") })
        {
            var path = Path.Combine(root, name);
            Assert.True(File.Exists(path), $"{name} is not in {root}. {PackageEvidence.HowToProduce}");
            Assert.True(sums.ContainsKey(name), $"{name} has no published SHA-256.");
            Assert.Equal(sums[name], Sha256Of(path));
        }

        Assert.Equal(sums.Count, sums.Keys.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// An x64 artifact carries x64 code. Managed assemblies are architecture-neutral and say so in
    /// their header, so they are read for what they are rather than assumed.
    /// </summary>
    [Fact]
    public void Every_native_binary_in_the_package_is_x64()
    {
        var foreign = LayoutFiles()
            .Where(file => file.Extension is ".dll" or ".exe")
            .Select(file => (file, image: ReadPortableExecutable(file.FullName)))
            .Where(entry => entry.image is { Managed: false, Machine: not Amd64 })
            .Select(entry => $"{Relative(entry.file)} (machine 0x{entry.image!.Machine:X4})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            foreign.Length == 0,
            $"The x64 package carries binaries for other architectures: {string.Join(", ", foreign)}.");
    }

    [Fact]
    public void The_application_host_is_an_x64_executable()
    {
        var host = Path.Combine(LayoutRoot(), "ApSolutions.LocalMedia.Windows.exe");
        Assert.True(File.Exists(host), $"The package has no application host. {PackageEvidence.HowToProduce}");

        var image = ReadPortableExecutable(host);

        Assert.NotNull(image);
        Assert.Equal(Amd64, image!.Machine);
        Assert.False(image.Managed, "The host is expected to be the native apphost, not a managed launcher.");
    }

    /// <summary>
    /// Runtime payloads for architectures this package does not target are not merely wasted bytes:
    /// they are code the artifact ships and cannot run, and reviewers reasonably read their presence
    /// as a package built for something else.
    /// </summary>
    [Fact]
    public void No_runtime_payload_for_another_architecture_travels_with_the_package()
    {
        var layout = LayoutRoot();
        var strays = Directory
            .EnumerateDirectories(layout, "*", SearchOption.AllDirectories)
            .Where(directory => Path.GetFileName(directory) is "win-x86" or "win-arm64" or "win-arm")
            .Select(directory => Path.GetRelativePath(layout, directory))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            strays.Length == 0,
            $"These runtime payloads target another architecture: {string.Join(", ", strays)}.");
    }

    [Fact]
    public void Debug_symbols_and_project_files_stay_out_of_the_artifact()
    {
        var unwanted = LayoutFiles()
            .Where(file => file.Extension is ".pdb" or ".cs" or ".csproj" or ".user" or ".sln"
                || file.Name is "packages.lock.json")
            .Select(Relative)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(unwanted.Length == 0, $"The artifact ships development files: {string.Join(", ", unwanted)}.");
    }

    /// <summary>
    /// The licence conditions travel with the binary or they are not met. GPL-3.0-or-later and the
    /// third-party notices are part of the payload, in both languages, not a link somewhere else.
    /// </summary>
    [Fact]
    public void The_licence_and_the_third_party_notices_travel_inside_the_artifact()
    {
        var layout = LayoutRoot();

        foreach (var required in new[]
        {
            "LICENSE",
            "NOTICE",
            Path.Combine("licenses", "THIRD-PARTY-NOTICES.es.md"),
            Path.Combine("licenses", "THIRD-PARTY-NOTICES.en.md"),
        })
        {
            Assert.True(
                File.Exists(Path.Combine(layout, required)),
                $"{required} is not in the artifact, so the licence conditions do not travel with it.");
        }
    }

    /// <summary>
    /// The text of every third-party licence travels too, not only a table naming them.
    /// </summary>
    /// <remarks>
    /// LGPL-2.1 §6, GPL-2.0 §1 and Apache-2.0 §4a each require a copy of the licence to accompany the
    /// binary, and MIT and BSD-3-Clause require the copyright notice to be reproduced. VideoLAN's
    /// NuGet package carries no <c>COPYING</c>, so what this artifact does not carry, nobody carries.
    /// </remarks>
    [Fact]
    public void The_text_of_every_third_party_licence_travels_inside_the_artifact()
    {
        var missing = PackageEvidence.LicenceTextsMissingFrom(LayoutRoot());

        Assert.True(
            missing.Length == 0,
            $"The artifact names licences it does not accompany: {string.Join("; ", missing)}.");
    }

    /// <summary>
    /// Nothing in the artifact may name the machine that built it or carry a credential. Only files
    /// this repository produces are read: a third-party binary can hold any byte sequence, and the
    /// promise being kept here is about what this project ships, not what it depends on.
    /// </summary>
    [Fact]
    public void Nothing_in_the_artifact_names_the_machine_that_built_it()
    {
        var layout = LayoutRoot();
        var offenders = new List<string>();

        foreach (var file in LayoutFiles().Where(IsOursOrText))
        {
            var text = File.ReadAllText(file.FullName);
            foreach (var marker in PrivacyMarkers.Where(marker =>
                text.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                offenders.Add($"{Path.GetRelativePath(layout, file.FullName)} contains '{marker}'");
            }
        }

        Assert.True(offenders.Count == 0, $"The artifact leaks local detail: {string.Join("; ", offenders)}.");
    }

    /// <summary>
    /// The artifact carries no access token. Identification reaches the network only when someone puts
    /// one in the environment by hand, and a token baked into the package would take that decision
    /// away from whoever installs it.
    /// </summary>
    [Fact]
    public void The_artifact_carries_no_access_token_of_any_kind()
    {
        var report = PackageEvidence.ReadReport("contents.json");

        Assert.True(
            report.TryGetProperty("secretScan", out var scan),
            "contents.json records no secret scan.");
        Assert.Equal(0, scan.GetProperty("hits").GetInt32());
        Assert.True(
            scan.GetProperty("filesScanned").GetInt32() > 0,
            "The secret scan examined nothing, so its zero means nothing.");
    }

    /// <summary>
    /// An SBOM that lists no components is a file, not a bill of materials. Both formats are produced
    /// because the two ecosystems that consume them read different ones.
    /// </summary>
    [Theory]
    [InlineData("sbom/sbom.cyclonedx.json", "components")]
    [InlineData("sbom/sbom.spdx.json", "packages")]
    public void The_sbom_lists_the_dependencies_the_artifact_actually_ships(string relativePath, string collection)
    {
        var path = Path.Combine(PackageEvidence.PackageRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"{relativePath} is missing. {PackageEvidence.HowToProduce}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var entries = document.RootElement.GetProperty(collection);

        Assert.True(
            entries.GetArrayLength() >= 10,
            $"{relativePath} lists {entries.GetArrayLength()} entries, which cannot describe this artifact.");
        foreach (var entry in entries.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.RequiredString("name")));
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty(
                collection == "components" ? "version" : "versionInfo").GetString()));
        }
    }

    /// <summary>
    /// Every package the application resolves has to appear in the bill of materials. A dependency
    /// that ships and is not listed is the one an audit will not find.
    /// </summary>
    [Fact]
    public void The_sbom_covers_every_package_the_application_resolves()
    {
        var report = PackageEvidence.ReadReport("contents.json");
        var missing = report.GetProperty("sbomGaps").EnumerateArray().Select(gap => gap.GetString()).ToArray();

        Assert.True(missing.Length == 0, $"These resolved packages are absent from the SBOM: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// The package and the layout are the same payload, so nothing can be verified on one and shipped
    /// on the other.
    /// </summary>
    [Fact]
    public void The_package_contains_exactly_what_the_verified_layout_contains()
    {
        var report = PackageEvidence.ReadReport("contents.json");
        var onlyInLayout = report.GetProperty("onlyInLayout").EnumerateArray().Select(entry => entry.GetString()).ToArray();
        var onlyInPackage = report.GetProperty("onlyInPackage").EnumerateArray().Select(entry => entry.GetString()).ToArray();

        Assert.True(onlyInLayout.Length == 0, $"Verified but not shipped: {string.Join(", ", onlyInLayout)}.");
        Assert.True(onlyInPackage.Length == 0, $"Shipped but not verified: {string.Join(", ", onlyInPackage)}.");
    }

    /// <summary>
    /// The package passed the validation that decides whether Windows could install it.
    /// </summary>
    /// <remarks>
    /// MakeAppx will happily seal a package it knows Windows would reject if asked to skip
    /// validation, and skipping it is one flag. That validation is the closest thing to an install
    /// this machine can run — it checks the manifest's protocols and file type associations, and that
    /// every declared file is really there — so throwing it away would leave the artifact's
    /// installability resting on nothing at all.
    /// </remarks>
    [Fact]
    public void The_package_passed_the_validation_that_decides_whether_windows_could_install_it()
    {
        var report = PackageEvidence.ReadReport("contents.json");

        Assert.True(
            report.GetProperty("semanticValidation").GetBoolean(),
            "The package was sealed without the validation that ensures Windows could install it.");
    }

    /// <summary>
    /// The report above cannot be trusted on its own: it writes a constant, so putting <c>/nv</c>
    /// back would leave it saying <c>true</c> while the validation no longer ran. The claim is only
    /// worth anything if the command behind it is checked too.
    /// </summary>
    [Fact]
    public void The_packaging_script_cannot_quietly_skip_that_validation()
    {
        var script = File.ReadAllText(Path.Combine(PackageEvidence.RepositoryRoot(), "eng", "package-x64.ps1"));
        var sealing = script
            .Split('\n')
            .Where(line => line.Contains("makeAppx pack", StringComparison.Ordinal)
                || line.Contains("makeappx pack", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(sealing);
        Assert.All(sealing, line => Assert.DoesNotContain("/nv", line, StringComparison.OrdinalIgnoreCase));
        Assert.All(sealing, line => Assert.DoesNotContain("/noValidation", line, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// There is no paid certificate behind this release. Saying so in the artifact report is what
    /// keeps the SmartScreen documentation honest.
    /// </summary>
    [Fact]
    public void The_artifact_declares_that_it_is_unsigned_rather_than_implying_otherwise()
    {
        var report = PackageEvidence.ReadReport("contents.json");

        Assert.False(report.GetProperty("signed").GetBoolean());
        Assert.Contains("SmartScreen", report.RequiredString("signingNote"), StringComparison.Ordinal);
    }

    private static string LayoutRoot() => Path.Combine(PackageEvidence.PackageRoot(), "layout");

    private static IEnumerable<FileInfo> LayoutFiles()
    {
        var layout = LayoutRoot();
        if (!Directory.Exists(layout))
        {
            throw new DirectoryNotFoundException(
                $"The package layout is not at {layout}. {PackageEvidence.HowToProduce}");
        }

        return new DirectoryInfo(layout).EnumerateFiles("*", SearchOption.AllDirectories);
    }

    private static string Relative(FileInfo file) => Path.GetRelativePath(LayoutRoot(), file.FullName);

    private static PortableExecutableHeader? ReadPortableExecutable(string path) =>
        PortableExecutableHeader.Read(path);

    private static bool IsOursOrText(FileInfo file) =>
        file.Name.StartsWith("ApSolutions.", StringComparison.Ordinal)
        || TextLike.Contains(file.Extension, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> ReadChecksums(string path)
    {
        Assert.True(File.Exists(path), $"SHA256SUMS.txt is missing. {PackageEvidence.HowToProduce}");
        return File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split(' ', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(parts => parts[1].TrimStart('*'), parts => parts[0], StringComparer.Ordinal);
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLower(CultureInfo.InvariantCulture);
    }

}
