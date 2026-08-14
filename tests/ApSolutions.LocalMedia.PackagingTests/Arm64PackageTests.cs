// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The ARM64 artifact, asked the same questions as the x64 one and one more: that it is the same
/// application rather than a second one that happens to share a name.
/// </summary>
/// <remarks>
/// Parity is the point of this increment. An ARM64 build that shipped a different manifest identity,
/// a different file type list, or a different set of managed assemblies would install beside the x64
/// package instead of replacing it, and no amount of ARM64-native code would make that the same
/// product. So the checks here are deliberately paired: what must be different is the architecture,
/// and what must be identical is everything else.
/// </remarks>
public sealed class Arm64PackageTests
{
    private static readonly string[] ForeignRuntimeDirectories = ["win-x86", "win-x64", "win-arm"];

    [Fact]
    public void The_arm64_packaging_script_exists_and_is_the_one_the_evidence_names()
    {
        var script = Path.Combine(RepositoryLayout.Root, "eng", "package-arm64.ps1");

        Assert.True(
            File.Exists(script),
            "eng/package-arm64.ps1 does not exist, so there is no ARM64 artifact to verify.");
    }

    /// <summary>
    /// The same rule the x64 script is held to. <c>/nv</c> skips the validation that decides whether
    /// Windows could install the package, and a report claiming the validation ran is worth nothing
    /// if the command behind it stopped running it.
    /// </summary>
    [Fact]
    public void The_arm64_packaging_script_cannot_quietly_skip_the_install_validation()
    {
        var script = Path.Combine(RepositoryLayout.Root, "eng", "package-arm64.ps1");
        Assert.True(File.Exists(script), "eng/package-arm64.ps1 does not exist.");

        var sealing = File.ReadAllText(script)
            .Split('\n')
            .Where(line => line.Contains("makeAppx pack", StringComparison.Ordinal)
                || line.Contains("makeappx pack", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(sealing);
        Assert.All(sealing, line => Assert.DoesNotContain("/nv", line, StringComparison.OrdinalIgnoreCase));
        Assert.All(sealing, line => Assert.DoesNotContain("/noValidation", line, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Both_arm64_distribution_paths_exist_and_hash_to_what_the_release_publishes()
    {
        var contents = PackageEvidence.ReadArm64Report("contents.json");
        var root = PackageEvidence.Arm64PackageRoot();
        var sums = ReadChecksums(Path.Combine(root, "SHA256SUMS.txt"));

        Assert.Equal("win-arm64", contents.RequiredString("runtime"));

        foreach (var name in new[] { contents.RequiredString("msix"), contents.RequiredString("zip") })
        {
            var path = Path.Combine(root, name);
            Assert.True(File.Exists(path), $"{name} is not in {root}. {PackageEvidence.HowToProduceArm64}");
            Assert.True(sums.ContainsKey(name), $"{name} has no published SHA-256.");
            Assert.Equal(sums[name], Sha256Of(path));
        }
    }

    /// <summary>
    /// The artifact names its architecture. Two files called the same thing in one release directory
    /// is how someone ends up installing the wrong one and reporting a bug nobody can reproduce.
    /// </summary>
    [Fact]
    public void The_arm64_artifacts_are_named_for_the_architecture_they_carry()
    {
        var contents = PackageEvidence.ReadArm64Report("contents.json");
        var version = PackageEvidence.DeclaredVersion();

        Assert.Equal($"APSolutions.LocalMedia_{version}_arm64.msix", contents.RequiredString("msix"));
        Assert.Equal($"ApReelume-{version}-win-arm64.zip", contents.RequiredString("zip"));
    }

    /// <summary>
    /// An ARM64 artifact carries ARM64 code. This is the check that a cross-build cannot fake: the
    /// machine value comes from the COFF header of every binary the package ships.
    /// </summary>
    [Fact]
    public void Every_native_binary_in_the_arm64_package_is_arm64()
    {
        var foreign = LayoutFiles()
            .Where(file => file.Extension is ".dll" or ".exe")
            .Select(file => (file, image: PortableExecutableHeader.Read(file.FullName)))
            .Where(entry => entry.image is { Managed: false } && entry.image.Machine != PortableExecutableHeader.Arm64)
            .Select(entry => $"{Relative(entry.file)} ({PortableExecutableHeader.NameOf(entry.image!.Machine)})")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            foreign.Length == 0,
            $"The ARM64 package carries binaries for other architectures: {string.Join(", ", foreign)}.");
    }

    [Fact]
    public void The_arm64_application_host_is_an_arm64_executable()
    {
        var host = Path.Combine(LayoutRoot(), "ApSolutions.LocalMedia.Windows.exe");
        Assert.True(File.Exists(host), $"The ARM64 package has no application host. {PackageEvidence.HowToProduceArm64}");

        var image = PortableExecutableHeader.Read(host);

        Assert.NotNull(image);
        Assert.Equal(PortableExecutableHeader.Arm64, image!.Machine);
        Assert.False(image.Managed, "The host is expected to be the native apphost, not a managed launcher.");
    }

    /// <summary>
    /// The mirror of the x64 rule. A win-x64 LibVLC payload inside the ARM64 package is code the
    /// artifact ships and the loader on that machine will never open.
    /// </summary>
    [Fact]
    public void No_runtime_payload_for_another_architecture_travels_with_the_arm64_package()
    {
        var layout = LayoutRoot();
        var strays = Directory
            .EnumerateDirectories(layout, "*", SearchOption.AllDirectories)
            .Where(directory => ForeignRuntimeDirectories.Contains(Path.GetFileName(directory), StringComparer.Ordinal))
            .Select(directory => Path.GetRelativePath(layout, directory))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            strays.Length == 0,
            $"These runtime payloads target another architecture: {string.Join(", ", strays)}.");
    }

    /// <summary>
    /// LibVLC is resolved by architecture at startup, so the ARM64 payload has to be under the name
    /// the loader will look for. A package with the right binaries in the wrong folder starts and
    /// then cannot play anything.
    /// </summary>
    [Fact]
    public void The_arm64_libvlc_payload_is_where_the_loader_looks_for_it()
    {
        var libvlc = Path.Combine(LayoutRoot(), "libvlc", "win-arm64");

        Assert.True(
            Directory.Exists(libvlc),
            "libvlc/win-arm64 is not in the payload, so LibVLCSharp would find no engine on an ARM64 machine.");
        Assert.True(
            File.Exists(Path.Combine(libvlc, "libvlc.dll")),
            "libvlc/win-arm64 has no libvlc.dll.");
        Assert.True(
            Directory.Exists(Path.Combine(libvlc, "plugins")),
            "libvlc/win-arm64 ships no plugins, so the engine would load and decode nothing.");
    }

    /// <summary>
    /// The manifest Windows reads declares the architecture it is for, and nothing else about the
    /// package's identity moves with it.
    /// </summary>
    [Fact]
    public void The_packaged_arm64_manifest_declares_arm64_and_keeps_the_identity_of_the_release()
    {
        var identity = PackagedManifestIdentity();
        var source = PackageEvidence.Manifest().Root!;
        var sourceIdentity = source.Elements().First(element => element.Name.LocalName == "Identity");

        Assert.Equal("arm64", (string?)identity.Attribute("ProcessorArchitecture"));
        Assert.Equal((string?)sourceIdentity.Attribute("Name"), (string?)identity.Attribute("Name"));
        Assert.Equal((string?)sourceIdentity.Attribute("Publisher"), (string?)identity.Attribute("Publisher"));
        Assert.Equal(
            PackageEvidence.PackageVersionFor(PackageEvidence.DeclaredVersion()),
            (string?)identity.Attribute("Version"));
    }

    /// <summary>
    /// Everything Windows acts on besides the architecture is the same file. The write virtualisation
    /// switches are in that list on purpose: they are what keeps an uninstall from deleting the
    /// library, and an ARM64 package that lost them would delete it on ARM64 machines only.
    /// </summary>
    [Fact]
    public void The_arm64_manifest_differs_from_the_x64_one_in_the_architecture_and_nothing_else()
    {
        var packaged = PackagedManifest();
        var source = PackageEvidence.Manifest();

        var packagedIdentity = packaged.Root!.Elements().First(element => element.Name.LocalName == "Identity");
        var sourceIdentity = source.Root!.Elements().First(element => element.Name.LocalName == "Identity");
        packagedIdentity.SetAttributeValue("ProcessorArchitecture", "x64");

        Assert.Equal(
            Normalise(source.Root!),
            Normalise(packaged.Root!));
        Assert.Equal("x64", (string?)sourceIdentity.Attribute("ProcessorArchitecture"));
    }

    [Fact]
    public void The_arm64_package_passed_the_validation_that_decides_whether_windows_could_install_it()
    {
        var report = PackageEvidence.ReadArm64Report("contents.json");

        Assert.True(
            report.GetProperty("semanticValidation").GetBoolean(),
            "The ARM64 package was sealed without the validation that ensures Windows could install it.");
    }

    [Fact]
    public void The_arm64_artifact_declares_that_it_is_unsigned_rather_than_implying_otherwise()
    {
        var report = PackageEvidence.ReadArm64Report("contents.json");

        Assert.False(report.GetProperty("signed").GetBoolean());
        Assert.Contains("SmartScreen", report.RequiredString("signingNote"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_arm64_artifact_carries_no_access_token_and_names_no_machine()
    {
        var report = PackageEvidence.ReadArm64Report("contents.json");
        var scan = report.GetProperty("secretScan");

        Assert.Equal(0, scan.GetProperty("hits").GetInt32());
        Assert.True(
            scan.GetProperty("filesScanned").GetInt32() > 0,
            "The secret scan examined nothing, so its zero means nothing.");
    }

    [Fact]
    public void The_arm64_sbom_covers_every_package_the_application_resolves()
    {
        var report = PackageEvidence.ReadArm64Report("contents.json");
        var missing = report.GetProperty("sbomGaps").EnumerateArray().Select(gap => gap.GetString()).ToArray();

        Assert.True(
            missing.Length == 0,
            $"These resolved packages are absent from the ARM64 SBOM: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void The_arm64_package_contains_exactly_what_the_verified_layout_contains()
    {
        var report = PackageEvidence.ReadArm64Report("contents.json");
        var onlyInLayout = report.GetProperty("onlyInLayout").EnumerateArray().Select(entry => entry.GetString()).ToArray();
        var onlyInPackage = report.GetProperty("onlyInPackage").EnumerateArray().Select(entry => entry.GetString()).ToArray();

        Assert.True(onlyInLayout.Length == 0, $"Verified but not shipped: {string.Join(", ", onlyInLayout)}.");
        Assert.True(onlyInPackage.Length == 0, $"Shipped but not verified: {string.Join(", ", onlyInPackage)}.");
    }

    /// <summary>
    /// The two packages carry the same application. Managed assemblies are architecture-neutral, so
    /// the set of them is what "same application" means in files: a name present in one and not the
    /// other is a feature that exists on one architecture only.
    /// </summary>
    [Fact]
    public void The_two_architectures_ship_the_same_managed_application()
    {
        var parity = PackageEvidence.ReadArm64Report("contents.json").GetProperty("parityWithX64");

        Assert.True(
            parity.GetProperty("compared").GetBoolean(),
            $"The ARM64 build did not compare itself with the x64 payload: {parity.RequiredString("reason")}");

        var onlyInX64 = parity.GetProperty("onlyInX64").EnumerateArray().Select(entry => entry.GetString()).ToArray();
        var onlyInArm64 = parity.GetProperty("onlyInArm64").EnumerateArray().Select(entry => entry.GetString()).ToArray();

        Assert.True(onlyInX64.Length == 0, $"Shipped on x64 only: {string.Join(", ", onlyInX64)}.");
        Assert.True(onlyInArm64.Length == 0, $"Shipped on ARM64 only: {string.Join(", ", onlyInArm64)}.");
    }

    /// <summary>
    /// The native code the two architectures do not share is written down rather than passed over.
    /// VideoLAN builds a different plugin set per architecture, and which plugins are missing decides
    /// what the application can do on that machine — the Quick Sync decoder and the OpenGL outputs
    /// have no ARM64 build, and that is a fact about the product, not a build detail.
    /// </summary>
    [Fact]
    public void The_native_code_the_architectures_do_not_share_is_recorded()
    {
        var parity = PackageEvidence.ReadArm64Report("contents.json").GetProperty("parityWithX64");

        foreach (var field in new[] { "nativeOnlyInX64", "nativeOnlyInArm64" })
        {
            Assert.True(
                parity.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Array,
                $"The parity report does not list {field}, so a difference in native payload would be invisible.");
        }
    }

    /// <summary>
    /// The licence conditions travel with this binary too. They are a condition of shipping the
    /// artifact, not a property of one architecture's build script.
    /// </summary>
    [Fact]
    public void The_licence_and_the_third_party_notices_travel_inside_the_arm64_artifact()
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
                $"{required} is not in the ARM64 artifact, so the licence conditions do not travel with it.");
        }
    }

    /// <summary>
    /// And so does the text of each third-party licence. The obligation is a property of shipping a
    /// binary, so the architecture that gets built second cannot be the one that ships without it.
    /// </summary>
    [Fact]
    public void The_text_of_every_third_party_licence_travels_inside_the_arm64_artifact()
    {
        var missing = PackageEvidence.LicenceTextsMissingFrom(LayoutRoot());

        Assert.True(
            missing.Length == 0,
            $"The ARM64 artifact names licences it does not accompany: {string.Join("; ", missing)}.");
    }

    private static string LayoutRoot() => Path.Combine(PackageEvidence.Arm64PackageRoot(), "layout");

    private static IEnumerable<FileInfo> LayoutFiles()
    {
        var layout = LayoutRoot();
        if (!Directory.Exists(layout))
        {
            throw new DirectoryNotFoundException(
                $"The ARM64 package layout is not at {layout}. {PackageEvidence.HowToProduceArm64}");
        }

        return new DirectoryInfo(layout).EnumerateFiles("*", SearchOption.AllDirectories);
    }

    private static string Relative(FileInfo file) => Path.GetRelativePath(LayoutRoot(), file.FullName);

    /// <summary>The manifest as it was sealed, extracted from the package rather than read from source.</summary>
    private static XDocument PackagedManifest()
    {
        var path = Path.Combine(PackageEvidence.Arm64PackageRoot(), "packaged", "AppxManifest.xml");
        Assert.True(
            File.Exists(path),
            $"The sealed ARM64 manifest was not extracted. {PackageEvidence.HowToProduceArm64}");
        return XDocument.Load(path);
    }

    private static XElement PackagedManifestIdentity() =>
        PackagedManifest().Root!.Elements().First(element => element.Name.LocalName == "Identity");

    /// <summary>Comparable text for an XML tree, so a whitespace difference is not read as a change.</summary>
    private static string Normalise(XElement element) =>
        element.ToString(SaveOptions.DisableFormatting);

    private static Dictionary<string, string> ReadChecksums(string path)
    {
        Assert.True(File.Exists(path), $"SHA256SUMS.txt is missing. {PackageEvidence.HowToProduceArm64}");
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
