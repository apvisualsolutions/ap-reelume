// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// <c>eng/verify-docs.ps1</c> against repositories made for it: one that must pass, and one per way
/// a published link can break for a reader while the script still says yes.
/// </summary>
/// <remarks>
/// <para>
/// Asked of this checkout the script only ever answers "passed", which is also what it would answer
/// if it had stopped reading. Each scene below holds one defect the script once let through:
/// </para>
/// <list type="bullet">
/// <item>a document outside <c>docs/</c> and the root was never read, so its links were never
/// checked — <c>eng/README-sandbox.md</c> is published and was invisible;</item>
/// <item>links were compared ignoring case, and GitHub serves paths as written, so a link that only
/// resolves on a case-insensitive disk is broken for everybody reading the repository online;</item>
/// <item>an image without alternative text did not match the link pattern, so its target was never
/// looked for;</item>
/// <item>a file the ignore rules exclude but that is still in the index counted as published, which
/// is not what the other published-file sweeps of this repository call published.</item>
/// </list>
/// <para>
/// The clean scene is the control for all of them: it holds one of each kind of link done right, so
/// a refusal below cannot come from a fixture the script would refuse anyway.
/// </para>
/// </remarks>
public sealed class VerifyDocsScriptTests
{
    [Fact]
    public void A_repository_whose_links_all_resolve_passes()
    {
        using var scene = new DocsScene();

        var (exitCode, output) = scene.Verify();

        Assert.True(exitCode == 0, $"verify-docs.ps1 refused the clean scene, so no refusal below can be believed: {output}");
        Assert.Contains("passed (git)", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_broken_link_in_a_document_outside_docs_and_the_root_is_reported()
    {
        using var scene = new DocsScene();
        scene.Write("eng/README-tool.md", "# Tool\n\nSee [the missing page](missing.md).\n");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Broken or unpublished link in eng/README-tool.md: missing.md", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_that_only_resolves_when_case_is_ignored_is_reported()
    {
        using var scene = new DocsScene();
        scene.Write("README.md", "# Scene\n\nThe [guide](docs/Guide.en.md).\n");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Broken or unpublished link in README.md: docs/Guide.en.md", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_image_without_alternative_text_is_still_checked()
    {
        using var scene = new DocsScene();
        scene.Write("README.md", "# Scene\n\n![](docs/missing.png)\n");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Broken or unpublished link in README.md: docs/missing.png", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_to_a_file_the_ignore_rules_exclude_is_reported_although_the_index_still_holds_it()
    {
        using var scene = new DocsScene();
        scene.Write("docs/internal.md", "kept on this machine\n");
        scene.Git("add", "docs/internal.md");
        scene.Write(".git/info/exclude", "/docs/internal.md\n");
        scene.Write("README.md", "# Scene\n\nThe [notes](docs/internal.md).\n");

        var (exitCode, output) = scene.Verify();

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Broken or unpublished link in README.md: docs/internal.md", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A git repository holding a pair of guides that link to each other, a root document that links
    /// to one of them, and an image referenced without alternative text: everything resolves, with the
    /// case written as it is on disk.
    /// </summary>
    private sealed class DocsScene : IDisposable
    {
        private readonly string _root =
            Path.Combine(Path.GetTempPath(), "verify-docs-" + Guid.NewGuid().ToString("n")[..12]);

        public DocsScene()
        {
            Directory.CreateDirectory(_root);
            Git("init", "--quiet");
            Write("README.md", "# Scene\n\nThe [guide](docs/guide.en.md).\n\n![](docs/logo.png)\n");
            Write("docs/guide.en.md", "# Guide\n\n[Español](guide.es.md)\n");
            Write("docs/guide.es.md", "# Guía\n\n[English](guide.en.md)\n");
            Write("docs/logo.png", "not really an image\n");
            Write("eng/README-tool.md", "# Tool\n\nBack to the [guide](../docs/guide.en.md).\n");
        }

        public void Write(string relative, string text)
        {
            var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }

        /// <summary>The script this repository ships, pointed at the scene.</summary>
        public (int ExitCode, string Output) Verify()
        {
            var start = new ProcessStartInfo("pwsh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in new[]
            {
                "-NoProfile", "-File", RepositoryLayout.PathFromRoot("eng/verify-docs.ps1"),
                "-RepositoryRoot", _root, "-MinimumDocuments", "2", "-MinimumLocalized", "2",
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("pwsh did not start; the scene cannot be measured without it.");
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(120_000);

            return (process.ExitCode, output + errors.Result);
        }

        public void Git(params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in (string[])["-c", "safe.directory=*", "-C", _root, .. arguments])
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)
                ?? throw new InvalidOperationException("git did not start; the scene cannot be built without it.");
            var errors = process.StandardError.ReadToEndAsync();
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {errors.Result}");
        }

        public void Dispose()
        {
            try
            {
                // Git writes its objects read-only, and a recursive delete refuses a read-only file.
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
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
    }
}
