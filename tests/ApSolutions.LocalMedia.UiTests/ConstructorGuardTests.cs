// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.TestSupport;

using Xunit;

namespace ApSolutions.LocalMedia.UiTests;

/// <summary>
/// Every view model in this assembly refuses a null dependency. Twenty-seven of the hundred and
/// five Presentation files short of the coverage bar wrote the guard and were never handed a null,
/// which is the same untaken branch <c>ConstructorGuardTests</c> sweeps out of Application.
/// </summary>
/// <remarks>
/// Presentation passed this the first time it ran: 112 parameters guarded and not one constructor
/// accepting a null, so there is no debt list here and adding one would be inventing a place for
/// future debt to hide.
/// </remarks>
public sealed class ConstructorGuardTests
{
    /// <summary>
    /// One sweep, shared by every test below. It is pure reflection over an assembly and builds
    /// nothing that survives the call, so running it three times measured the same answer three
    /// times -- and this suite runs beside a headless Avalonia session, where less concurrent work
    /// is worth having for its own sake.
    /// </summary>
    private static readonly Lazy<ConstructorGuardSweep.Sweep> Swept =
        new(() => ConstructorGuardSweep.Run(typeof(ShellViewModel).Assembly));

    /// <summary>
    /// The floor that keeps this from passing by seeing nothing — reflection goes quiet rather than
    /// red when it stops matching. It reached 112 parameters on 2026-08-30.
    /// </summary>
    private const int LeastParametersTheSweepMustReach = 105;

    [Fact]
    public void Every_view_model_refuses_a_null_dependency()
    {
        var sweep = Swept.Value;

        Assert.Empty(sweep.Unbuildable);
        Assert.True(
            sweep.Unguarded.Count == 0,
            "These view models accept a null dependency, so they fail at first use instead of at "
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
