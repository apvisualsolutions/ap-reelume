// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// One owner for the native LibVLC instance, and one place that counts it (BUG-010).
/// </summary>
/// <remarks>
/// <c>LibVlcFactory</c> states that the process keeps exactly one native instance per option set,
/// and <c>NativeInstanceCount</c> is what states it. Both were false while
/// <c>LibVlcMediaProbe</c> built its own with the same three options: a process that catalogued and
/// played held two, and the count could not see the second because it only knows what the factory
/// created. A count that cannot see what it counts is worse than no count.
/// <para>
/// This is a source rule rather than a runtime one on purpose: at runtime the second instance is
/// invisible, which is the whole defect. The rule is cheap to satisfy — ask the factory — and it
/// fails loudly the moment a second owner appears.
/// </para>
/// </remarks>
public sealed class NativeInstanceOwnershipTests
{
    /// <summary>The one file allowed to construct the native instance and initialise the core.</summary>
    private const string Owner = "LibVlcFactory.cs";

    [Fact]
    public void Only_the_factory_constructs_the_native_instance()
    {
        var offenders = SourceFilesMatching(@"new\s+LibVLC\s*\(");

        Assert.True(
            offenders.Length == 0,
            "The native LibVLC instance has exactly one owner, and these build their own: "
                + string.Join(", ", offenders)
                + $". Take a {Owner[..^3]} and ask it for the media instead.");
    }

    [Fact]
    public void Only_the_factory_initialises_the_native_core()
    {
        var offenders = SourceFilesMatching(@"Core\.Initialize\s*\(");

        Assert.True(
            offenders.Length == 0,
            "Core.Initialize belongs to the instance owner alone; these call it too: "
                + string.Join(", ", offenders) + ".");
    }

    /// <summary>
    /// One deferred-release queue, minus the offenders still named here. A copy of the queue is not
    /// a copy of the same care: the factory's drain survives a native dispose that throws, and the
    /// copies do not — their worker flag stays raised, so the first failing release ends the worker
    /// for good and every media after it leaks, silently.
    /// </summary>
    /// <remarks>
    /// The list works like the orphan list in <see cref="ServiceConsumptionTests"/>: it may only
    /// shrink. <c>LibVlcMediaPlayerEngine</c> is on it because its unification is not the same
    /// change — its <c>DisposeAsync</c> awaits its own drain before releasing the player, and that
    /// order is what keeps the native teardown from crashing, so moving it to the shared queue needs
    /// the factory to be able to flush on request. It was found by this rule, not by reading, and it
    /// is named rather than exempted quietly.
    /// </remarks>
    [Fact]
    public void The_deferred_release_queue_has_one_implementation_and_it_survives_a_throwing_dispose()
    {
        string[] stillOwnTheirOwn = ["src/ApSolutions.LocalMedia.Infrastructure/Playback/LibVlcMediaPlayerEngine.cs"];

        var offenders = SourceFilesMatching(@"Queue<\s*DeferredMedia\s*>");

        Assert.True(
            offenders.Except(stillOwnTheirOwn, StringComparer.Ordinal).ToArray() is [],
            "The deferred media release is the factory's; these keep a queue of their own: "
                + string.Join(", ", offenders.Except(stillOwnTheirOwn, StringComparer.Ordinal)) + ".");
        Assert.True(
            stillOwnTheirOwn.Except(offenders, StringComparer.Ordinal).ToArray() is [],
            "These are declared as still keeping their own queue and no longer do: "
                + string.Join(", ", stillOwnTheirOwn.Except(offenders, StringComparer.Ordinal))
                + ". Remove them from the list — it is only allowed to shrink.");

        var factory = File.ReadAllText(Path.Combine(SourceRoot, "Playback", Owner));
        Assert.Contains(
            "catch (Exception exception) when (exception is not OutOfMemoryException)",
            factory,
            StringComparison.Ordinal);
    }

    private static string SourceRoot { get; } =
        RepositoryLayout.PathFromRoot("src/ApSolutions.LocalMedia.Infrastructure");

    private static string[] SourceFilesMatching(string pattern) =>
    [
        .. Directory
            .EnumerateFiles(RepositoryLayout.PathFromRoot("src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).Equals(Owner, StringComparison.Ordinal))
            .Where(file => Regex.IsMatch(
                File.ReadAllText(file),
                pattern,
                RegexOptions.None,
                TimeSpan.FromSeconds(2)))
            .Select(file => Path.GetRelativePath(RepositoryLayout.Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal),
    ];
}
