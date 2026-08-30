// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.TestSupport;

using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests;

/// <summary>
/// Every adapter in this assembly refuses a null dependency. Twenty-six of the forty-one
/// Infrastructure files short of the coverage bar wrote the guard and were never handed a null,
/// and Infrastructure is where a missing dependency matters most: these are the types that hold the
/// database connection, the file system and the network.
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
        new(() => ConstructorGuardSweep.Run(typeof(MigrationRunner).Assembly));

    /// <summary>
    /// The one constructor that still accepts a null, with the reason, kept the way
    /// <c>PendingWiring</c> is kept in <c>ServiceConsumptionTests</c>: a debt with a name on it,
    /// forced out by <see cref="The_list_names_only_constructors_that_still_accept_a_null"/> as soon
    /// as its guard lands.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> AcceptsNull =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ApSolutions.LocalMedia.Infrastructure.Data.IntegrityChecker"] = "primary constructor",
        };

    /// <summary>
    /// The floor that keeps this from passing by seeing nothing. It reached 64 parameters on
    /// 2026-08-30.
    /// </summary>
    private const int LeastParametersTheSweepMustReach = 60;

    [Fact]
    public void Every_adapter_refuses_a_null_dependency()
    {
        var sweep = Swept.Value;

        Assert.Empty(sweep.Unbuildable);

        var unexpected = sweep.Unguarded.Where(type => !AcceptsNull.ContainsKey(type)).ToArray();
        Assert.True(
            unexpected.Length == 0,
            "These adapters accept a null dependency and are on no list, so they fail at first use "
            + "instead of at construction: " + string.Join(", ", unexpected));
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
