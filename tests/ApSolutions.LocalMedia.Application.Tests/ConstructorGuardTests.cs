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
/// throw was a branch nothing took, which is why <c>StartPlayback</c>, <c>StopPlayback</c> and four
/// others sat at exactly 100 lines and 50 branches.
/// </summary>
/// <remarks>
/// There is no list of exceptions here, and there was one for a day. Seven constructors declared
/// their dependencies as a primary constructor, which has nowhere to put a guard without a field,
/// so they took the null and failed later at first use. They are explicit constructors now and the
/// list is gone — which is what a debt list is for. The rule is structural again: every constructor,
/// no exemptions, nothing to keep true by hand.
/// </remarks>
public sealed class ConstructorGuardTests
{
    /// <summary>
    /// One sweep, shared by every test below. It is pure reflection over an assembly and builds
    /// nothing that survives the call, so running it three times measured the same answer three
    /// times.
    /// </summary>
    private static readonly Lazy<ConstructorGuardSweep.Sweep> Swept =
        new(() => ConstructorGuardSweep.Run(typeof(StopPlayback).Assembly));

    /// <summary>
    /// The floor that keeps this suite from passing by seeing nothing. A sweep is reflection over an
    /// assembly, so a rename or a filter that stops matching turns it green in silence rather than
    /// red — the failure mode this repository has measured more than any other. It reached 127
    /// parameters on 2026-08-30 and 148 once the seven primary constructors were promoted; the floor
    /// sits below that so retiring a use case is not a red, and far enough above zero that going
    /// blind is.
    /// </summary>
    private const int LeastParametersTheSweepMustReach = 140;

    [Fact]
    public void Every_constructor_refuses_a_null_dependency()
    {
        var sweep = Swept.Value;

        Assert.Empty(sweep.Unbuildable);
        Assert.True(
            sweep.Unguarded.Count == 0,
            "These constructors accept a null dependency, so whatever they are given they will fail "
            + "at first use instead of at construction: " + string.Join(", ", sweep.Unguarded));
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
