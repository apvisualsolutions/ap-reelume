// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// Every project in this tree is in the solution, because the solution is what every gate builds.
/// </summary>
/// <remarks>
/// The release-signing tool sat outside it and stopped compiling: its <c>Program.cs</c> had no
/// licence header, <c>IDE0073</c> is an error here, and nothing ever built the file to say so —
/// <c>dotnet build …sln -warnaserror</c> cannot fail on a project the solution does not contain.
/// The release workflow runs that tool with <c>dotnet run</c>, so the first thing that would have
/// compiled it was a real publication, at the step that verifies the signature.
/// <para>
/// A project outside the solution is this repository's characteristic defect in its build: it exists,
/// something depends on it, and no gate reaches it. The rule is cheap — add the project — and it
/// fails the moment another one appears.
/// </para>
/// <para>
/// <b>A project git ignores is not asked for, but only when it is on a closed list.</b> A working
/// copy can hold a project that is never published, and demanding it in the solution would make the
/// solution name a project a clone cannot restore, which breaks every build there. Exempting
/// whatever git ignores was the first version of this rule, and it was blind: the benchmark project
/// compiles a fixture of the performance suite, a change to that fixture broke it, and nothing said
/// so, because an ignored project was simply not looked at. So each project allowed to stay out is
/// named here, and one that is not named fails like a forgotten one. Whether a named one still
/// compiles is checked where it exists.
/// </para>
/// </remarks>
public sealed class SolutionCoverageTests
{
    /// <summary>
    /// The projects that git ignores in some working copies and that may stay out of the solution,
    /// with forward slashes. Nothing else may.
    /// </summary>
    /// <remarks>
    /// The benchmarks are run by hand and are not part of the published tree. They link
    /// <c>Catalog10kBuilder.cs</c> from the performance suite, which is why their build is not left to
    /// chance.
    /// </remarks>
    internal static readonly string[] IgnoredAndAllowed =
    [
        "benchmarks/ApSolutions.LocalMedia.Benchmarks/ApSolutions.LocalMedia.Benchmarks.csproj",
    ];

    private const string NotAllowedToStayOut = " (ignored by git, and not one of the projects allowed to stay out)";

    [Fact]
    public void Every_project_in_the_tree_is_built_by_the_solution()
    {
        var missing = ProjectsOutsideTheSolution(
            RepositoryLayout.Root,
            File.ReadAllText(RepositoryLayout.SolutionPath),
            IgnoredAndAllowed);

        Assert.True(
            missing is [],
            "These projects are in the tree and not in the solution, so no gate builds them: "
                + string.Join(", ", missing)
                + ". Add them, or the first thing to compile them will be whatever runs them.");
    }

    /// <summary>
    /// The rule against a repository made for it. Of the six projects there, one is named by the
    /// solution, one is ignored and on the list, and one belongs to another checkout nested inside:
    /// those three are silent. The other three must be reported — one forgotten, one ignored and not
    /// on the list, and one still in the index although a rule ignores it, which a clone receives
    /// all the same.
    /// </summary>
    /// <remarks>
    /// Asked of this checkout the rule only ever answers "nothing is missing", which is also what it
    /// would answer if it had stopped looking. The scene is where it has to say something.
    /// </remarks>
    [Fact]
    public void A_forgotten_project_is_reported_and_only_a_listed_ignored_one_is_not()
    {
        var root = Path.Combine(Path.GetTempPath(), "solution-coverage-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            Git(root, "init", "--quiet");
            Write(root, "src/Named/Named.csproj", "<Project />\n");
            Write(root, "src/Forgotten/Forgotten.csproj", "<Project />\n");
            Write(root, "kept-out/Tool/Tool.csproj", "<Project />\n");
            Write(root, "kept-out/Stray/Stray.csproj", "<Project />\n");
            Write(root, "tracked-out/Old/Old.csproj", "<Project />\n");
            Git(root, "add", "tracked-out/Old/Old.csproj");
            Write(root, ".gitignore", "/kept-out/\n/tracked-out/\n");
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            Git(Path.Combine(root, "nested"), "init", "--quiet");
            Write(root, "nested/Other/Other.csproj", "<Project />\n");
            const string solution = @"Project(""{FAE04EC0}"") = ""Named"", ""src\Named\Named.csproj"", ""{1}""";

            Assert.Equal(
                [
                    Path.Combine("kept-out", "Stray", "Stray.csproj") + NotAllowedToStayOut,
                    Path.Combine("src", "Forgotten", "Forgotten.csproj"),
                    Path.Combine("tracked-out", "Old", "Old.csproj"),
                ],
                ProjectsOutsideTheSolution(root, solution, ["kept-out/Tool/Tool.csproj"]));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                // Git writes its objects read-only, and a recursive delete refuses a read-only file.
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] ProjectsOutsideTheSolution(string root, string solution, IReadOnlyCollection<string> ignoredAndAllowed)
    {
        var projects = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))

            // Build output never holds a project of its own; skipping it keeps the ignore query short.
            .Where(relative => !relative.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "bin" or "obj"))
            .Where(relative => !IsInsideAnotherCheckout(root, relative))
            .ToArray();

        var ignored = Ignored(root, projects);
        return
        [
            .. projects
                .Select(relative => (Relative: relative, Slashed: relative.Replace('\\', '/')))
                .Where(project => ignored.Contains(project.Slashed)
                    ? !ignoredAndAllowed.Contains(project.Slashed, StringComparer.Ordinal)
                    : !solution.Contains(project.Relative.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase))
                .Select(project => ignored.Contains(project.Slashed) ? project.Relative + NotAllowedToStayOut : project.Relative)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Whether a directory between <paramref name="root"/> and the project holds its own <c>.git</c>:
    /// a nested clone or a worktree is another checkout, answerable to its own solution.
    /// </summary>
    private static bool IsInsideAnotherCheckout(string root, string relative)
    {
        var directory = root;
        foreach (var segment in Path.GetDirectoryName(relative)!.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            directory = Path.Combine(directory, segment);
            if (Path.Exists(Path.Combine(directory, ".git")))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The paths among these that git ignores, with forward slashes.</summary>
    /// <remarks>
    /// Outside a repository nothing is ignored, and the rule asks for every project it finds.
    /// A project still in the index is never ignored here, even when a rule matches it: ignoring
    /// stops a file from being added and never removes one, so a clone still receives it and the
    /// solution has to build it. That is why the question goes to the index and not around it.
    /// </remarks>
    private static HashSet<string> Ignored(string root, string[] relativePaths)
    {
        if (relativePaths.Length == 0
            || (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git"))))
        {
            return [];
        }

        var start = new ProcessStartInfo("git")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-c", "safe.directory=*", "-c", "core.quotepath=off", "-C", root, "check-ignore", "--stdin", "-z"])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("git could not be started to ask what it ignores.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        foreach (var path in relativePaths)
        {
            process.StandardInput.Write(path.Replace('\\', '/') + "\0");
        }

        process.StandardInput.Close();
        process.WaitForExit();

        // check-ignore exits 1 when nothing matched, which is an answer; anything else is a failure
        // that would otherwise read as "nothing is ignored" and ask for projects a clone lacks.
        if (process.ExitCode is not (0 or 1))
        {
            throw new InvalidOperationException($"git check-ignore exited {process.ExitCode}: {errors.Result}");
        }

        return output.Result.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
    }

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static void Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in (string[])["-c", "safe.directory=*", "-C", root, .. arguments])
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var errors = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', arguments)}: {errors.Result}");
    }
}
