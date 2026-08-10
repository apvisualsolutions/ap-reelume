// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests;

/// <summary>
/// The composition as text, however many files it is spread across.
/// </summary>
/// <remarks>
/// A dozen wiring tests assert on the composition's source because they check invocation halves no
/// service descriptor can express — a lambda calling into a view model, a startup hook — and that is
/// the accepted style until ARQ-006 finishes. Each of them used to open `CompositionRoot.cs` by name,
/// which quietly made "the composition" mean "one file": splitting the registration into modules
/// (ARQ-006 step 2) turned eight of those tests red without a single wire changing. Reading every
/// `CompositionRoot*.cs` keeps the assertions about the composition rather than about its filing.
/// </remarks>
internal static class CompositionSourceText
{
    private static readonly Lazy<string> Cached = new(Load);

    /// <summary>Every partial of the composition, concatenated in a stable order.</summary>
    public static string Read() => Cached.Value;

    private static string Load()
    {
        var directory = Path.Combine(RepositoryRoot(), "src", "ApSolutions.LocalMedia.Windows");
        var files = Directory.GetFiles(directory, "CompositionRoot*.cs")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            files.Length > 0,
            "No CompositionRoot source was found where the host keeps it.");
        return string.Join("\n", files.Select(File.ReadAllText));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
