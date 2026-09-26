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
/// <b>A project git ignores is not the repository's</b>, so it is not asked for. A working copy can
/// hold projects that are never published — a runner's unpacked actions, or a machine's own tools
/// kept out with local exclude rules — and a clone has none of them. Demanding them in the solution
/// would make the solution name projects a clone cannot restore, which breaks every build there.
/// </para>
/// </remarks>
public sealed class SolutionCoverageTests
{
    [Fact]
    public void Every_project_in_the_tree_is_built_by_the_solution()
    {
        var missing = ProjectsOutsideTheSolution(
            RepositoryLayout.Root,
            File.ReadAllText(RepositoryLayout.SolutionPath));

        Assert.True(
            missing is [],
            "These projects are in the tree and not in the solution, so no gate builds them: "
                + string.Join(", ", missing)
                + ". Add them, or the first thing to compile them will be whatever runs them.");
    }

    /// <summary>
    /// The rule against a repository made for it: one project the solution names, one it forgets,
    /// and one git ignores. Only the forgotten one may be reported.
    /// </summary>
    /// <remarks>
    /// Asked of this checkout the rule only ever answers "nothing is missing", which is also what it
    /// would answer if it had stopped looking. The scene is where it has to say something.
    /// </remarks>
    [Fact]
    public void A_forgotten_project_is_reported_and_an_ignored_one_is_not()
    {
        var root = Path.Combine(Path.GetTempPath(), "solution-coverage-" + Guid.NewGuid().ToString("n")[..12]);
        Directory.CreateDirectory(root);
        try
        {
            Git(root, "init", "--quiet");
            Write(root, ".gitignore", "/kept-out/\n");
            Write(root, "src/Named/Named.csproj", "<Project />\n");
            Write(root, "src/Forgotten/Forgotten.csproj", "<Project />\n");
            Write(root, "kept-out/Tool/Tool.csproj", "<Project />\n");
            const string solution = @"Project(""{FAE04EC0}"") = ""Named"", ""src\Named\Named.csproj"", ""{1}""";

            Assert.Equal([@"src\Forgotten\Forgotten.csproj"], ProjectsOutsideTheSolution(root, solution));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] ProjectsOutsideTheSolution(string root, string solution)
    {
        var projects = Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))

            // Build output never holds a project of its own; skipping it keeps the ignore query short.
            .Where(relative => !relative.Split(Path.DirectorySeparatorChar).Any(segment =>
                segment is "bin" or "obj"))
            .ToArray();

        var ignored = Ignored(root, projects);
        return
        [
            .. projects
                .Where(relative => !ignored.Contains(relative.Replace('\\', '/')))
                .Where(relative => !solution.Contains(
                    relative.Replace('/', '\\'),
                    StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>The paths among these that git ignores, with forward slashes.</summary>
    /// <remarks>
    /// Outside a repository nothing is ignored, and the rule asks for every project it finds.
    /// <c>--no-index</c> because a project still in the index but already matched by the ignore
    /// rules is on its way out of the repository, and a clone will not have it either.
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
        foreach (var argument in (string[])["-c", "safe.directory=*", "-c", "core.quotepath=off", "-C", root, "check-ignore", "--no-index", "--stdin", "-z"])
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
