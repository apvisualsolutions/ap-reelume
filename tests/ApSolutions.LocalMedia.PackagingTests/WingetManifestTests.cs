using System.IO.Compression;
using System.Text.RegularExpressions;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The package manager entry, checked against the archive it claims to install.
/// </summary>
/// <remarks>
/// winget is the one channel that costs nothing and needs no certificate, which makes it the first
/// one a person is likely to use. It is also the one where a mistake is least visible: winget
/// downloads what the manifest names, checks the hash the manifest states, and runs the file the
/// manifest points at inside the archive. Get any of those three wrong and it fails on somebody
/// else's machine, quietly, after a hundred megabytes.
/// <para>
/// So none of the three is trusted here. The hash is compared against the one that was published,
/// the executable is looked for inside the real archive, and the version against the one this
/// repository builds.
/// </para>
/// </remarks>
public sealed class WingetManifestTests
{
    private const string Identifier = "APSolutions.APReelume";

    /// <summary>
    /// The hash winget checks has to be the hash that was published. Two records that can disagree
    /// are one record and one rumour, and this one fails on the machine of whoever installs.
    /// </summary>
    [Fact]
    public void The_manifest_states_the_hash_that_was_actually_published()
    {
        var version = PackageEvidence.DeclaredVersion();
        var archive = $"ApReelume-{version}-win-x64.zip";
        var published = File
            .ReadAllLines(Path.Combine(PackageEvidence.PackageRoot(), "SHA256SUMS.txt"))
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Single(parts => parts.Length == 2 && parts[1].Equals(archive, StringComparison.OrdinalIgnoreCase))[0];

        Assert.Equal(published.ToLowerInvariant(), Value("installer", "InstallerSha256"));
        Assert.EndsWith($"/{archive}", Value("installer", "InstallerUrl"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The file winget runs after extracting has to exist inside the archive. Naming one that is not
    /// there produces an install that reports success and leaves nothing runnable behind.
    /// </summary>
    [Fact]
    public void The_executable_the_manifest_points_at_is_really_inside_the_archive()
    {
        var relative = Value("installer", "RelativeFilePath");
        var archivePath = Path.Combine(
            PackageEvidence.PackageRoot(),
            $"ApReelume-{PackageEvidence.DeclaredVersion()}-win-x64.zip");

        using var archive = ZipFile.OpenRead(archivePath);
        Assert.True(
            archive.Entries.Any(entry => entry.FullName == relative),
            $"The winget manifest runs '{relative}', which the archive does not contain.");
    }

    /// <summary>
    /// A zip is not an installer, so winget has to be told what is inside it and how to run it.
    /// Without both fields the submission is rejected; with the wrong ones it installs nothing.
    /// </summary>
    [Fact]
    public void The_archive_is_declared_as_an_archive_carrying_a_portable_application()
    {
        Assert.Equal("zip", Value("installer", "InstallerType"));
        Assert.Equal("portable", Value("installer", "NestedInstallerType"));
        Assert.False(string.IsNullOrWhiteSpace(Value("installer", "PortableCommandAlias")));
    }

    /// <summary>
    /// ARM64 is built and verified on every run and is not published until `PRD-003` is settled. A
    /// package manager entry is publication, so it must not appear here either — the decision is not
    /// upheld by remembering it.
    /// </summary>
    [Fact]
    public void Only_the_architecture_that_is_published_appears_in_the_manifest()
    {
        var installer = Manifest("installer");

        Assert.Contains("Architecture: x64", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("arm64", installer, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Every part of the entry describes this release: the version this repository builds, and a
    /// description in both languages that says different things in each.
    /// </summary>
    [Fact]
    public void The_entry_describes_this_release_in_both_languages()
    {
        var version = PackageEvidence.DeclaredVersion();

        foreach (var part in new[] { "version", "installer", "locale.en-US", "locale.es-ES" })
        {
            Assert.Equal(version, Value(part, "PackageVersion"));
            Assert.Equal(Identifier, Value(part, "PackageIdentifier"));
        }

        var english = Value("locale.en-US", "ShortDescription");
        var spanish = Value("locale.es-ES", "ShortDescription");
        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.False(string.IsNullOrWhiteSpace(spanish));
        Assert.NotEqual(english, spanish);
        Assert.All([english, spanish], text => Assert.True(text.Length <= 256, "winget allows 256 characters."));
        Assert.Equal("GPL-3.0-or-later", Value("locale.en-US", "License"));
    }

    /// <summary>
    /// Nothing here was typed by hand, and the file says so. A generated manifest somebody edits is
    /// one that stops matching the artifact the moment the next release is cut.
    /// </summary>
    [Fact]
    public void The_manifest_declares_that_it_was_generated()
    {
        foreach (var part in new[] { "version", "installer", "locale.en-US", "locale.es-ES" })
        {
            Assert.StartsWith("# Generated by eng/build-winget-manifest.ps1", Manifest(part), StringComparison.Ordinal);
        }
    }

    private static string Manifest(string part)
    {
        var name = part == "version" ? $"{Identifier}.yaml" : $"{Identifier}.{part}.yaml";
        var path = Path.Combine(
            PackageEvidence.PackageRoot(),
            "winget",
            "manifests",
            "a",
            "APSolutions",
            "APReelume",
            PackageEvidence.DeclaredVersion(),
            name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"{name} was not generated, so nothing is known about what a package manager would "
                + $"install. {PackageEvidence.HowToProduce}",
                path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>Reads one scalar out of a manifest, wherever it sits in the nesting.</summary>
    private static string Value(string part, string key)
    {
        var match = Regex.Match(
            Manifest(part),
            $@"(?m)^\s*-?\s*{Regex.Escape(key)}:\s*(?<value>.+?)\s*$",
            RegexOptions.None,
            TimeSpan.FromSeconds(1));
        Assert.True(match.Success, $"The {part} manifest declares no {key}.");
        return match.Groups["value"].Value;
    }
}
