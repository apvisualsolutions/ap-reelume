// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.TestSupport;

using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests;

/// <summary>
/// Every use case in this assembly refuses a null dependency, and this is where that is measured.
/// The guard is written in thirty-six of the forty-five Application files that were short of the
/// coverage bar, and until this sweep existed not one of them had ever been handed a null: the
/// throw was a branch nothing took, which is why <c>StartPlayback</c>, <c>StopPlayback</c> and six
/// others sat at exactly 100 lines and 50 branches.
/// </summary>
public sealed class ConstructorGuardTests
{
    /// <summary>
    /// One sweep, shared by every test below. It is pure reflection over an assembly and builds
    /// nothing that survives the call, so running it three times measured the same answer three
    /// times -- and this suite runs beside a headless Avalonia session, where less concurrent work
    /// is worth having for its own sake.
    /// </summary>
    private static readonly Lazy<ConstructorGuardSweep.Sweep> Swept =
        new(() => ConstructorGuardSweep.Run(typeof(StopPlayback).Assembly));

    /// <summary>
    /// The constructors that still accept a null, each with the reason. All seven declare their
    /// dependencies as a primary constructor, which has nowhere to put a guard without a field, so
    /// they take the null and fail later at the first use — a NullReferenceException from inside a
    /// method instead of an ArgumentNullException from the composition that caused it.
    /// </summary>
    /// <remarks>
    /// This works like <c>PendingWiring</c> in <c>ServiceConsumptionTests</c>: an entry is a debt
    /// with a name on it, not an exemption. <see cref="The_list_names_only_constructors_that_still_accept_a_null"/>
    /// forces an entry out the moment its guard lands, so the list can only shrink truthfully.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> AcceptsNull =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApSolutions.LocalMedia.Application.Discovery.ExecuteRename"] = "primary constructor",
            ["ApSolutions.LocalMedia.Application.Discovery.PreviewRename"] = "primary constructor",
            ["ApSolutions.LocalMedia.Application.Discovery.UndoRename"] = "primary constructor",
            ["ApSolutions.LocalMedia.Application.Identification.ApplyIdentification"] = "primary constructor",
            ["ApSolutions.LocalMedia.Application.Metadata.RefreshMetadata"] = "primary constructor",
            ["ApSolutions.LocalMedia.Application.Metadata.RefreshStaleMetadata"] = "primary constructor",
            ["ApSolutions.LocalMedia.Application.Metadata.UpdateMetadata"] = "primary constructor",
        };

    /// <summary>
    /// The floor that keeps this suite from passing by seeing nothing. A sweep is reflection over an
    /// assembly, so a rename or a filter that stops matching turns it green in silence rather than
    /// red — the failure mode this repository has measured more than any other. It reached 127
    /// parameters on 2026-08-30; the floor sits below that so retiring a use case is not a red, and
    /// far enough above zero that going blind is.
    /// </summary>
    private const int LeastParametersTheSweepMustReach = 120;

    [Fact]
    public void Every_constructor_refuses_a_null_dependency()
    {
        var sweep = Swept.Value;

        Assert.Empty(sweep.Unbuildable);

        var unexpected = sweep.Unguarded.Where(type => !AcceptsNull.ContainsKey(type)).ToArray();
        Assert.True(
            unexpected.Length == 0,
            "These constructors accept a null dependency and are on no list, so whatever they are "
            + "given they will fail at first use instead of at construction: "
            + string.Join(", ", unexpected));
    }

    [Fact]
    public void The_list_names_only_constructors_that_still_accept_a_null()
    {
        var sweep = Swept.Value;

        var stale = AcceptsNull.Keys
            .Where(type => !sweep.Unguarded.Contains(type))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "These constructors now refuse a null; take them out of the list so the debt stays "
            + "true: " + string.Join(", ", stale));
    }

    [Fact]
    public void The_sweep_still_reaches_the_constructors_it_guards()
    {
        var sweep = Swept.Value;

        Assert.True(
            sweep.Guarded.Count >= LeastParametersTheSweepMustReach,
            $"The sweep refused a null in only {sweep.Guarded.Count} parameters, under the "
            + $"{LeastParametersTheSweepMustReach} it has to reach. Either the guards are gone or "
            + "the sweep stopped seeing them; both are failures and neither is quiet.");
    }
}
