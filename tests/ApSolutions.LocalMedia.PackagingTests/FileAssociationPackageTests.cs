// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// What the package tells Windows about itself. Identity, architecture, version, and the containers
/// the application offers to open are all promises made in one file, and the file is the only place
/// Windows ever reads them from.
/// </summary>
/// <remarks>
/// The association list exists three times — in the domain, in the authored fragment, and in the
/// manifest — because each one is read by something different. Three copies drift, so the suite pins
/// them to each other rather than to a literal list of its own.
/// </remarks>
public sealed class FileAssociationPackageTests
{
    private static readonly XNamespace Foundation = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private static readonly XNamespace Uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
    private static readonly XNamespace Rescap = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";
    private static readonly XNamespace Desktop6 = "http://schemas.microsoft.com/appx/manifest/desktop/windows10/6";

    [Fact]
    public void The_package_keeps_the_identity_the_product_was_designed_around()
    {
        var identity = Identity();

        Assert.Equal("APSolutions.LocalMedia", identity.Attribute("Name")?.Value);
        Assert.Equal("x64", identity.Attribute("ProcessorArchitecture")?.Value);
    }

    /// <summary>
    /// One SemVer, one package version, derived rather than typed twice. A manifest that drifts from
    /// the build is a package that claims to be a release nobody produced.
    /// </summary>
    [Fact]
    public void The_package_version_is_the_declared_semver_with_the_reserved_revision()
    {
        var expected = PackageEvidence.PackageVersionFor(PackageEvidence.DeclaredVersion());

        var declared = Identity().Attribute("Version")?.Value;

        Assert.Equal(expected, declared);
        Assert.EndsWith(".0", declared, StringComparison.Ordinal);
    }

    [Fact]
    public void The_manifest_starts_the_executable_the_build_produces()
    {
        var application = Assert.Single(PackageEvidence.Manifest().Descendants(Foundation + "Application"));

        Assert.Equal("ApSolutions.LocalMedia.Windows.exe", application.Attribute("Executable")?.Value);
        Assert.Equal("windows.fullTrustApplication", application.Attribute("EntryPoint")?.Value);
    }

    /// <summary>
    /// The containers the manifest offers must be the containers the scanner recognises. A file the
    /// library would catalogue has to be a file "Open with…" will play, and the reverse.
    /// </summary>
    [Fact]
    public void The_package_offers_exactly_the_containers_the_domain_approves()
    {
        var declared = ManifestFileTypes();

        Assert.Equal(MediaFileExtensions.All, declared);
    }

    [Fact]
    public void The_authored_fragment_and_the_manifest_do_not_drift_apart()
    {
        var fragment = XDocument.Load(Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Windows",
            "Packaging",
            "FileAssociations.xml"));

        var fromFragment = fragment
            .Descendants(Uap + "FileType")
            .Select(type => type.Value)
            .ToArray();

        Assert.Equal(ManifestFileTypes(), fromFragment);
    }

    /// <summary>
    /// The association is an offer, not a seizure: Windows lists the application under "Open with…"
    /// and the user decides. A package that declares itself the default handler has taken a decision
    /// that belongs to whoever installs it.
    /// </summary>
    [Fact]
    public void The_association_never_claims_to_be_the_default_handler()
    {
        var association = Assert.Single(PackageEvidence.Manifest().Descendants(Uap + "FileTypeAssociation"));

        Assert.DoesNotContain(
            association.Descendants(),
            element => element.Name.LocalName is "SupportedVerbs" or "EditFlags");
        Assert.Null(association.Attribute("DesiredView"));
    }

    /// <summary>
    /// A full-trust desktop package already has every right the process needs, so any further
    /// capability is a claim the product does not have to make. Declaring none is what makes the
    /// privacy promise readable from the manifest alone.
    /// </summary>
    /// <summary>
    /// Two capabilities, both of them about how the application runs rather than about what it may
    /// reach: <c>runFullTrust</c> because it is a desktop application, and
    /// <c>unvirtualizedResources</c> because without it Windows redirects its writes into a container
    /// the uninstall deletes. Neither grants access to anything of the user's, and the list is pinned
    /// exactly so a third one cannot arrive unnoticed.
    /// </summary>
    [Fact]
    public void The_package_asks_for_nothing_beyond_running_as_a_desktop_application()
    {
        var manifest = PackageEvidence.Manifest();
        var capabilities = manifest
            .Descendants()
            .Where(element => element.Name.LocalName == "Capability")
            .ToArray();

        Assert.Equal(
            ["runFullTrust", "unvirtualizedResources"],
            capabilities.Select(element => element.Attribute("Name")?.Value).Order(StringComparer.Ordinal));
        Assert.All(capabilities, element => Assert.Equal(Rescap, element.Name.Namespace));
        Assert.DoesNotContain(manifest.Descendants(), element => element.Name.LocalName == "DeviceCapability");
    }

    /// <summary>
    /// The package writes where it says it writes, and its data outlives it.
    /// </summary>
    /// <remarks>
    /// Installing the package on a clean machine showed that MSIX redirects an application's writes
    /// under AppData into the package container: the library landed in
    /// <c>…\Packages\&lt;family&gt;\LocalCache\Local\…</c> rather than where every document promises,
    /// and uninstalling took the whole container — catalogue, progress, and backups — with it. That is
    /// data loss on an ordinary uninstall, and no unpacked run could see it.
    /// <para>
    /// Turning the virtualisation off is a property of the manifest rather than of the code, because
    /// the redirection happens below the application: it would rewrite any path the code chose.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("FileSystemWriteVirtualization")]
    [InlineData("RegistryWriteVirtualization")]
    public void The_package_does_not_let_windows_redirect_its_writes_into_the_container(string property)
    {
        var declared = PackageEvidence.Manifest()
            .Descendants()
            .SingleOrDefault(element => element.Name.LocalName == property);

        Assert.True(
            declared is not null,
            $"The manifest does not declare {property}, so an uninstall would delete the user's library.");
        Assert.Equal("disabled", declared!.Value);
        Assert.Equal(Desktop6, declared.Name.Namespace);
    }

    /// <summary>
    /// The manifest inside the built package is what Windows reads. Verifying the authored one and
    /// trusting the pipeline would leave the only file that matters unchecked.
    /// </summary>
    [Fact]
    public void The_manifest_inside_the_built_package_carries_the_same_associations()
    {
        var packaged = PackagedManifest();
        var declared = packaged
            .Descendants(Uap + "FileType")
            .Select(type => type.Value)
            .ToArray();

        Assert.Equal(MediaFileExtensions.All, declared);
        Assert.Equal(
            PackageEvidence.PackageVersionFor(PackageEvidence.DeclaredVersion()),
            packaged.Descendants(Foundation + "Identity").Single().Attribute("Version")?.Value);
    }

    private static XElement Identity() =>
        Assert.Single(PackageEvidence.Manifest().Descendants(Foundation + "Identity"));

    private static string[] ManifestFileTypes() =>
        [.. PackageEvidence.Manifest().Descendants(Uap + "FileType").Select(type => type.Value)];

    private static XDocument PackagedManifest()
    {
        var report = PackageEvidence.ReadReport("contents.json");
        var manifestPath = Path.Combine(PackageEvidence.PackageRoot(), report.RequiredString("packagedManifest"));
        Assert.True(
            File.Exists(manifestPath),
            $"The manifest extracted from the package is not at {manifestPath}. {PackageEvidence.HowToProduce}");
        return XDocument.Load(manifestPath);
    }
}
