// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;

using NSubstitute;

using Xunit;

namespace ApSolutions.LocalMedia.Application.Tests.Discovery;

/// <summary>
/// The three rename use cases, built with real dependencies in this suite.
/// </summary>
/// <remarks>
/// <para>
/// They are exercised in <c>IntegrationTests</c> too, and that is exactly why these tests exist. A
/// guard whose two sides are taken by two different suites reads as half covered forever: merged
/// Cobertura keeps the better report for a line rather than the union of them, so a null handed in
/// by the constructor sweep here and a real dependency handed in over there never add up. The three
/// files measured 100/50, 100/75 and 100/83 in CI for that reason alone, with both sides genuinely
/// taken. <c>ReviewInboxViewModel</c> hit the same wall on 2026-08-28.
/// </para>
/// <para>
/// So the fix is not another assertion, it is the same assertion in the same place: this suite now
/// both refuses a null — through the sweep — and builds each use case with something real.
/// </para>
/// </remarks>
public sealed class RenameUseCaseTests
{
    [Fact]
    public void A_preview_plans_the_renames_without_touching_anything()
    {
        var preview = new PreviewRename(new RenamePolicy());

        var plan = preview.Execute(new PreviewRenameCommand(
            @"D:\library",
            [new RenameRequest(@"D:\library\arrival.mkv", "Arrival (2016).mkv")]));

        Assert.True(plan.CanExecute);
        Assert.Single(plan.Operations);
    }

    [Fact]
    public async Task Neither_executing_nor_undoing_reaches_the_renamer_without_consent()
    {
        var renamer = Substitute.For<ISafeFileRenamer>();
        var plan = new PreviewRename(new RenamePolicy()).Execute(new PreviewRenameCommand(
            @"D:\library",
            [new RenameRequest(@"D:\library\arrival.mkv", "Arrival (2016).mkv")]));

        var executed = await new ExecuteRename(renamer).ExecuteAsync(
            new ExecuteRenameCommand(plan, Confirmed: false),
            TestContext.Current.CancellationToken);
        var undone = await new UndoRename(renamer).ExecuteAsync(
            new UndoRenameCommand(plan, Confirmed: false),
            TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.NotConfirmed, executed.Outcome);
        Assert.Equal(RenameExecutionOutcome.NotConfirmed, undone.Outcome);
        await renamer.DidNotReceive().ExecuteAsync(Arg.Any<RenamePlan>(), Arg.Any<CancellationToken>());
        await renamer.DidNotReceive().UndoAsync(Arg.Any<RenamePlan>(), Arg.Any<CancellationToken>());
    }
}
