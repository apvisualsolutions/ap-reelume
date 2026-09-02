// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Every test that builds the shell's surfaces runs on Avalonia's dispatcher thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured 2026-09-02, and it is the rule two failures were paying for.</b> The headless suite
/// runs at <c>AvaloniaTestIsolationLevel.PerTest</c> — the package's own documentation says that is
/// the default when none is declared, and the measurement agreed: three consecutive
/// <c>[AvaloniaFact]</c>s saw three <b>different</b> <c>Application</c> instances. So every one of
/// this assembly's 400-odd headless tests creates an application, publishes it on
/// <c>Application.Current</c>, and tears it down again, while xunit runs the other collections in
/// parallel.
/// </para>
/// <para>
/// A plain <c>[Fact]</c> runs on an xunit worker thread instead, and <c>ShellSurfaces</c> reaches
/// <c>Application.Current</c> through <c>ShortcutSettingsViewModel</c> → <c>CourseText.Resource</c>.
/// It therefore reads an application some other test owns, is halfway through building, or has
/// already destroyed. Measured over six runs of a probe: on the dispatcher thread, 3.827.981 reads
/// and <b>not one failure</b>; on a worker thread, <b>four</b> <c>NullReferenceException</c>s inside
/// <c>Avalonia.Styling.Styles.TryGetResource</c> — the same stack CI reported.
/// </para>
/// <para>
/// The natural experiment was already in the tree: <c>EditorPageTests</c> and
/// <c>ShellWindowModeTests</c> build the <b>same</b> surfaces through <c>EditorSurfaces()</c>, have
/// always been headless, and have never failed. <c>ShellAssemblyTests</c> was the one that was not.
/// </para>
/// <para>
/// <b>The obvious guard was measured and thrown away.</b> Asserting
/// <c>Dispatcher.UIThread.CheckAccess()</c> inside the builder answers <c>True</c> on a plain
/// <c>[Fact]</c> as well — four runs, both kinds, always <c>True</c> — so it would have passed on
/// precisely the thread it was written to catch. Counting on <c>Application.Current</c> being null is
/// no better: it is null 99,4 % of the time there, which is a gate that is right most days.
/// </para>
/// </remarks>
public sealed class ShellSurfaceIsolationTests
{
    /// <summary>
    /// The classes that build shell surfaces. Closed on purpose: a fourth one is caught by
    /// <see cref="The_census_of_surface_builders_is_the_one_this_class_guards"/>, which reads the
    /// source rather than trusting this list.
    /// </summary>
    private static readonly Type[] Builders =
    [
        typeof(ShellAssemblyTests),
        typeof(EditorPageTests),
        typeof(ShellWindowModeTests),
    ];

    private const string SharedBuilder = "EditorSurfaces(";

    /// <summary>
    /// Not one plain <c>[Fact]</c> among them. This is the half that a regression trips: flipping a
    /// single attribute back turns it red, which the race it guards would only do now and then.
    /// </summary>
    [Fact]
    public void Every_test_that_builds_shell_surfaces_is_headless()
    {
        var offenders = new List<string>();

        foreach (var type in Builders)
        {
            var tests = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => (method, kinds: FactKinds(method)))
                .Where(pair => pair.kinds.Length > 0)
                .ToArray();

            // A floor per class, not one for the lot: a class that stopped declaring tests would let
            // the loop below walk past it without a word, and the census half would still be green.
            // And a floor rather than a count on purpose — a total would go red at every test added
            // to these classes, saying "expected 37, got 36" about a class that is about threads.
            Assert.NotEmpty(tests);

            offenders.AddRange(
                from pair in tests
                where !pair.kinds.All(kind => kind.StartsWith("Avalonia", StringComparison.Ordinal))
                select $"{type.Name}.{pair.method.Name} is [{string.Join("][", pair.kinds)}]");
        }

        // Named, not counted: "collection was not empty" would leave the next person opening
        // three files to find out which attribute moved.
        Assert.True(
            offenders.Count == 0,
            "These build shell surfaces off Avalonia's dispatcher thread: "
                + string.Join("; ", offenders));
    }

    /// <summary>
    /// The census, so the table above cannot go quietly out of date: every file in this suite that
    /// spends the shared builder is a file this class already names.
    /// </summary>
    /// <remarks>
    /// Read out of the source because that is where a call is written, and it fails in both
    /// directions — a consumer nobody listed, and a class listed that no longer builds anything.
    /// </remarks>
    [Fact]
    public void The_census_of_surface_builders_is_the_one_this_class_guards()
    {
        var root = RepositoryLayout.PathFromRoot("tests/ApSolutions.LocalMedia.UiTests");
        var spenders = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            // This class names the builder to look for, so it would otherwise count itself.
            var name = Path.GetFileNameWithoutExtension(file);
            if (name == nameof(ShellSurfaceIsolationTests))
            {
                continue;
            }

            if (File.ReadAllText(file).Contains(SharedBuilder, StringComparison.Ordinal))
            {
                _ = spenders.Add(name);
            }
        }

        // The floor first: a sweep that found nothing would agree with any table at all.
        Assert.NotEmpty(spenders);
        Assert.Equal(
            Builders.Select(type => type.Name).OrderBy(name => name, StringComparer.Ordinal),
            spenders);
    }

    private static string[] FactKinds(MethodInfo method) => method
        .GetCustomAttributes(inherit: false)
        .Select(attribute => attribute.GetType().Name)
        .Where(name => name.EndsWith("FactAttribute", StringComparison.Ordinal)
            || name.EndsWith("TheoryAttribute", StringComparison.Ordinal))
        .ToArray();
}
