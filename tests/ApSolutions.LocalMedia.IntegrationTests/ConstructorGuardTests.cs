// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.TestSupport;

using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests;

/// <summary>
/// Every adapter in this assembly refuses a null dependency. Twenty-six of the forty-one
/// Infrastructure files short of the coverage bar wrote the guard and were never handed a null, and
/// Infrastructure is where a missing dependency matters most: these are the types that hold the
/// database connection, the file system and the network.
/// </summary>
/// <remarks>
/// There is no list of exceptions here, and there was one for a day: <c>IntegrityChecker</c>
/// declared its connection factory as a primary constructor, which has nowhere to put a guard
/// without a field. It is an explicit constructor now and the list is gone.
/// </remarks>
public sealed class ConstructorGuardTests
{
    /// <summary>
    /// One sweep, shared by every test below. It is pure reflection over an assembly and builds
    /// nothing that survives the call, so running it three times measured the same answer three
    /// times.
    /// </summary>
    private static readonly Lazy<ConstructorGuardSweep.Sweep> Swept =
        new(() => ConstructorGuardSweep.Run(typeof(MigrationRunner).Assembly));

    /// <summary>
    /// The floor that keeps this from passing by seeing nothing. It reached 64 parameters on
    /// 2026-08-30 and 65 once IntegrityChecker was promoted.
    /// </summary>
    private const int LeastParametersTheSweepMustReach = 60;

    [Fact]
    public void Every_adapter_refuses_a_null_dependency()
    {
        var sweep = Swept.Value;

        Assert.Empty(sweep.Unbuildable);
        Assert.True(
            sweep.Unguarded.Count == 0,
            "These adapters accept a null dependency, so they fail at first use instead of at "
            + "construction: " + string.Join(", ", sweep.Unguarded));
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
