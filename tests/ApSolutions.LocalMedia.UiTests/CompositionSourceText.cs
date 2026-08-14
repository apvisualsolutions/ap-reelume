// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.TestSupport;
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
/// <para>
/// It happened a second time, and the same way. ARQ-001 moved the application's ownership out to
/// `Shell/ApplicationHost.cs`, so tests about wires that had not moved went red. The lesson stuck
/// rather than the patch: what this returns is everything that composes the application, and adding
/// a file to that set is part of moving code out of the root.
/// </para>
/// </remarks>
internal static class CompositionSourceText
{
    /// <summary>
    /// Where composition lives, relative to the host project. Order is stable so a failure message
    /// points at the same place twice.
    /// </summary>
    private static readonly (string Directory, string Pattern)[] Sources =
    [
        (".", "CompositionRoot*.cs"),
        ("Shell", "ApplicationHost.cs"),
    ];

    private static readonly Lazy<string> Cached = new(Load);

    /// <summary>Every file the application is composed from, concatenated in a stable order.</summary>
    public static string Read() => Cached.Value;

    private static string Load()
    {
        var host = Path.Combine(RepositoryLayout.Root, "src", "ApSolutions.LocalMedia.Windows");
        var files = Sources
            .SelectMany(source => Directory.GetFiles(Path.Combine(host, source.Directory), source.Pattern))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            files.Length >= Sources.Length,
            $"Only {files.Length} composition source(s) were found where the host keeps them.");
        return string.Join("\n", files.Select(File.ReadAllText));
    }

}
