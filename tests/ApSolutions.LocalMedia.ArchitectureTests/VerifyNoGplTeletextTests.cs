// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;
using System.Text.Json;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// <c>eng/libvlc/verify-nogpl.ps1</c> refuses a tree that carries libzvbi or a teletext decoder, and
/// refuses to vouch for one when its reference cannot show the canary.
/// </summary>
/// <remarks>
/// <para>
/// libzvbi 0.2.35 carries two GPL-2.0-only files, <c>packet-830.c</c> and <c>pdc.c</c>, and nothing
/// the gate had could see them: VLC's contrib recipe for it does not say REQUIRE_GPL, so
/// <c>contrib/bootstrap --disable-gpl</c> builds it, and the source scan reads VLC's own module,
/// which is LGPL. The first published tree shipped it. The substitute VLC builds when libzvbi is
/// absent, telx, carries code converted from a GPL decoder, so both are left out.
/// </para>
/// <para>
/// The scene is a lying tree rather than a real build because a real build is an hour inside
/// VideoLAN's images: every file here is a few bytes that say only what each check reads. The script
/// is the one this repository ships. Every refusal is asserted by its own message, and the clean tree
/// beside them passes, so a refusal cannot come from a fixture that fails for some other reason.
/// </para>
/// </remarks>
public sealed class VerifyNoGplTeletextTests
{
    /// <summary>The log prefix of libzvbi's misc.c, which the reference carries on both architectures.</summary>
    private const string ZvbiCanary = "libzvbi:%s: %s";

    /// <summary>
    /// The control for every refusal below: the scene, as built, is a tree the script accepts.
    /// </summary>
    [Fact]
    public void A_tree_without_zvbi_or_telx_passes()
    {
        using var scene = new NoGplScene();

        var (exitCode, output) = scene.Verify();

        Assert.True(exitCode == 0, $"verify-nogpl.ps1 refused the clean scene, so no refusal below can be believed: {output}");
    }

    [Fact]
    public void A_plugin_carrying_libzvbi_is_refused()
    {
        using var scene = new NoGplScene();
        scene.WriteOurPlugin("demux/libts_plugin.dll", "a demuxer with " + ZvbiCanary + " linked in");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("libzvbi is linked into: libts_plugin.dll", output, StringComparison.Ordinal);
    }

    /// <summary>A library can be linked into the core as well as into a plugin, so the core is read too.</summary>
    [Fact]
    public void A_core_carrying_libzvbi_is_refused()
    {
        using var scene = new NoGplScene();
        scene.WriteOurCore("libvlccore.dll", "a core with " + ZvbiCanary + " linked in");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("libzvbi is linked into: libvlccore.dll", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The name alone is enough: a libzvbi_plugin.dll whose library the canary does not recognise —
    /// another version, other strings — is still the module that links it.
    /// </summary>
    [Theory]
    [InlineData("codec/libzvbi_plugin.dll")]
    [InlineData("codec/libtelx_plugin.dll")]
    public void A_teletext_decoder_in_the_tree_is_refused(string plugin)
    {
        using var scene = new NoGplScene();
        scene.WriteOurPlugin(plugin, "a decoder the canary does not recognise");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains($"teletext decoders carrying GPL code are in the tree: {Path.GetFileName(plugin)}", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reference is VideoLAN's GPL build, which links libzvbi. If it stops showing the canary —
    /// the module gone, or the library's strings changed — a tree without the string proves nothing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_reference_that_cannot_show_the_canary_fails_as_blind(bool referenceHasThePlugin)
    {
        using var scene = new NoGplScene();
        scene.DeleteReferencePlugin("codec/libzvbi_plugin.dll");
        if (referenceHasThePlugin)
        {
            scene.WriteReferencePlugin("codec/libzvbi_plugin.dll", "a zvbi module with other strings");
        }

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("the zvbi canary is blind", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The manifest is the list a person reviews before a tree replaces the published one, and «not
    /// built» there reads as a plugin nobody wanted. These two were left out for their licence.
    /// </summary>
    [Fact]
    public void The_manifest_says_why_the_teletext_decoder_is_missing()
    {
        using var scene = new NoGplScene();

        var (exitCode, output) = scene.Verify();
        Assert.True(exitCode == 0, output);

        using var manifest = JsonDocument.Parse(File.ReadAllText(scene.ManifestPath));
        var reason = manifest.RootElement.GetProperty("missingVsReference").EnumerateArray()
            .Single(entry => entry.GetProperty("plugin").GetString() == "libzvbi_plugin")
            .GetProperty("reason").GetString();

        Assert.Equal("left out: carries GPL code", reason);
    }

    /// <summary>
    /// VideoLAN's package has no telx, so comparing against it can never say why telx is absent
    /// here. The manifest names both decoders the build leaves out, each with its reason.
    /// </summary>
    [Fact]
    public void The_manifest_names_both_teletext_decoders_left_out_on_purpose()
    {
        using var scene = new NoGplScene();

        var (exitCode, output) = scene.Verify();
        Assert.True(exitCode == 0, output);

        using var manifest = JsonDocument.Parse(File.ReadAllText(scene.ManifestPath));
        var disabled = manifest.RootElement.GetProperty("disabledOnPurpose").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("plugin").GetString()!, entry => entry.GetProperty("reason").GetString()!);

        Assert.Equal(["libtelx_plugin", "libzvbi_plugin"], disabled.Keys.Order(StringComparer.Ordinal));
        Assert.All(disabled.Values, reason => Assert.Contains("GPL", reason, StringComparison.Ordinal));
    }

    /// <summary>
    /// The tree is also published on its own, as a release asset, so it has to carry the licences
    /// that bind it: LGPL-2.1 for LibVLC, and LGPL-3.0 with the GPL-3.0 it builds on for the
    /// libraries linked inside five plugins, with a notice that names them. The manifest lists them,
    /// so a tree that lost one fails the fetch before it reaches anybody.
    /// </summary>
    [Fact]
    public void The_tree_carries_its_licences_and_its_manifest_lists_them()
    {
        using var scene = new NoGplScene();

        var (exitCode, output) = scene.Verify();
        Assert.True(exitCode == 0, output);

        string[] expected = ["licenses/GPL-3.0.txt", "licenses/LGPL-2.1.txt", "licenses/LGPL-3.0.txt", "licenses/NOTICE-LibVLC-contribs.txt", "licenses/NOTICE.txt"];
        using var manifest = JsonDocument.Parse(File.ReadAllText(scene.ManifestPath));
        var listed = manifest.RootElement.GetProperty("files").EnumerateArray()
            .Select(entry => entry.GetProperty("path").GetString()!)
            .Where(path => path.StartsWith("licenses/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        Assert.Equal(expected, listed);
        var destination = Path.GetDirectoryName(scene.ManifestPath)!;
        Assert.All(expected, path => Assert.True(new FileInfo(Path.Combine(destination, path)).Length > 1000, path));
    }
}

/// <summary>
/// What verify-nogpl.ps1 reads, and nothing else: a VLC source tree just deep enough for the licence
/// scan and its two controls, an install prefix, VideoLAN's reference with every canary the script
/// demands, and a destination.
/// </summary>
public sealed class NoGplScene : IDisposable
{
    private const string Lgpl = "/* This library is under the GNU Lesser General Public License. */\n";
    private const string Gpl = "/* This program is under the GNU General Public License. */\n";

    private readonly string _root;
    private readonly string _source;
    private readonly string _install;
    private readonly string _reference;

    public NoGplScene()
    {
        _root = Path.Combine(Path.GetTempPath(), "verify-nogpl-" + Guid.NewGuid().ToString("n")[..12]);
        _source = Path.Combine(_root, "vlc-src");
        _install = Path.Combine(_root, "install");
        _reference = Path.Combine(_root, "reference");

        // Every plugin either tree holds has to be declared, or the scan refuses to guess its licence.
        Write(_source, "modules/Makefile.am", string.Join('\n', [
            "libavcodec_plugin_la_SOURCES = codec/avcodec.c",
            "libdeinterlace_plugin_la_SOURCES = video_filter/deinterlace.c",
            "libfreetype_plugin_la_SOURCES = text_renderer/freetype.c",
            "libts_plugin_la_SOURCES = demux/ts.c",
            "libi420_rgb_sse2_plugin_la_SOURCES = video_chroma/i420_rgb.c",
            "libx264_plugin_la_SOURCES = codec/x264.c",
            "libzvbi_plugin_la_SOURCES = codec/zvbi.c",
            "libtelx_plugin_la_SOURCES = codec/telx.c",
            string.Empty,
        ]));
        foreach (var lgpl in new[] { "codec/avcodec.c", "video_filter/deinterlace.c", "text_renderer/freetype.c", "demux/ts.c", "codec/zvbi.c", "codec/telx.c" })
        {
            Write(_source, "modules/" + lgpl, Lgpl);
        }

        // The scan's two controls: a GPL file it must classify, and a plugin GPL only through an include.
        Write(_source, "modules/codec/x264.c", Gpl);
        Write(_source, "modules/video_chroma/i420_rgb.c", Lgpl + "#include \"i420_rgb_mmx.h\"\n");
        Write(_source, "modules/video_chroma/i420_rgb_mmx.h", Gpl);
        Write(_source, "contrib/contrib-x86_64/Makefile", "AD_CLAUSES := 1\n");
        Git("init --quiet");
        Git("-c user.email=scene@example.invalid -c user.name=scene -c commit.gpgsign=false commit --quiet --allow-empty -m base");

        WriteOurCore("libvlc.dll", "libvlc");
        WriteOurCore("libvlccore.dll", "libvlccore");
        WriteOurPlugin("codec/libavcodec_plugin.dll", "avcodec");
        WriteOurPlugin("video_filter/libdeinterlace_plugin.dll", "deinterlace without the GPL mode");
        WriteOurPlugin("text_renderer/libfreetype_plugin.dll", "freetype");
        WriteOurPlugin("demux/libts_plugin.dll", "ts");

        WriteReferencePlugin("codec/libavcodec_plugin.dll", "configuration: --enable-gpl");
        WriteReferencePlugin("video_chroma/libi420_rgb_sse2_plugin.dll", "i420 to rgb");
        WriteReferencePlugin("video_filter/libdeinterlace_plugin.dll", "yadif2x");
        WriteReferencePlugin("text_renderer/libfreetype_plugin.dll", "freetype");
        WriteReferencePlugin("demux/libts_plugin.dll", "arib parser was created");
        WriteReferencePlugin("codec/libzvbi_plugin.dll", "teletext through libzvbi:%s: %s");
    }

    public string ManifestPath => Path.Combine(_root, "destination", "manifest.json");

    public void WriteOurCore(string fileName, string content) =>
        Write(_install, "bin/" + fileName, content);

    public void WriteOurPlugin(string relativePath, string content) =>
        Write(_install, "lib/vlc/plugins/" + relativePath, content);

    public void WriteReferencePlugin(string relativePath, string content) =>
        Write(_reference, "plugins/" + relativePath, content);

    public void DeleteReferencePlugin(string relativePath) =>
        File.Delete(Path.Combine(_reference, "plugins", relativePath));

    /// <summary>Runs the script this repository ships over the scene; hands back its exit code and all it said.</summary>
    public (int ExitCode, string Output) Verify()
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = _root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "-NoProfile", "-File", RepositoryLayout.PathFromRoot("eng/libvlc/verify-nogpl.ps1"),
            "-Install", _install, "-VlcSource", _source, "-Reference", _reference,
            "-Destination", Path.Combine(_root, "destination"),
        })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("pwsh did not start; the scene cannot be measured without it.");
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(120_000);

        return (process.ExitCode, stdout + stderr.Result);
    }

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                // git leaves its objects read-only, and a read-only file stops the delete.
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Write(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void Git(string arguments)
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = _source,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git did not start; the scene cannot be built without it.");
        var stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit(60_000);
        Assert.True(process.ExitCode == 0, $"git {arguments} failed: {stderr.Result}");
    }
}
