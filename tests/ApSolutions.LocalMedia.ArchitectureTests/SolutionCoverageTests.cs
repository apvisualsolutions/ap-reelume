// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

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
/// </remarks>
public sealed class SolutionCoverageTests
{
    [Fact]
    public void Every_project_in_the_tree_is_built_by_the_solution()
    {
        var solution = File.ReadAllText(RepositoryLayout.SolutionPath);
        var missing = Directory
            .EnumerateFiles(RepositoryLayout.Root, "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RepositoryLayout.Root, path))

            // A working copy carries more than the sources: a runner unpacks third-party actions
            // under a dot-directory and those ship sample projects. Everything git ignores starts
            // with a dot at the top level, so that is the line — the same one the documentation
            // gates draw.
            .Where(relative => !relative.StartsWith('.'))
            .Where(relative => !solution.Contains(
                relative.Replace('/', '\\'),
                StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing is [],
            "These projects are in the tree and not in the solution, so no gate builds them: "
                + string.Join(", ", missing)
                + ". Add them, or the first thing to compile them will be whatever runs them.");
    }
}
