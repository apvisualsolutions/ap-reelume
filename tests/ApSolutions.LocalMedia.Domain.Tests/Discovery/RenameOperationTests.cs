// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

/// <summary>
/// The two computed members of the rename records, and the defaults the renamer reads.
///
/// RenamePolicyTests exercises these types through the policy, and the policy can only ever build
/// the half of each answer that says yes: it refuses a request whose destination equals its source
/// with a NoChange conflict, and it never returns a plan whose operations are empty and whose
/// conflicts are empty at the same time. So the negative arm of both guards had no test, and both
/// guards are load-bearing in Infrastructure — these records are public and SafeFileRenamer builds
/// them itself when it undoes a batch.
/// </summary>
public sealed class RenameOperationTests
{
    private static readonly string Root = Path.GetFullPath(@"C:\Media");

    /// <summary>
    /// IsCaseOnlyChange is not a description, it is a permission: SafeFileRenamer.ValidateExecutionState
    /// reports DestinationExists for an operation whose destination is already a file, and skips that
    /// check entirely when this property is true, because on NTFS a case-only rename always finds its
    /// own destination occupied. An operation whose source and destination are the same string would
    /// therefore be waved past the one check standing between it and overwriting a real file, so it
    /// has to answer false — and it does so on the first comparison, before the case-insensitive one
    /// is ever reached.
    /// </summary>
    [Fact]
    public void A_destination_equal_to_its_source_is_not_the_case_only_exception()
    {
        var source = Path.Combine(Root, "arrival.mkv");

        var unchanged = new RenameOperation(0, source, source);
        var caseOnly = new RenameOperation(1, source, Path.Combine(Root, "ARRIVAL.mkv"));
        var renamed = new RenameOperation(2, source, Path.Combine(Root, "Arrival (2016).mkv"));

        Assert.False(unchanged.IsCaseOnlyChange);
        Assert.True(caseOnly.IsCaseOnlyChange);
        Assert.False(renamed.IsCaseOnlyChange);
    }

    /// <summary>
    /// CanExecute asks two questions and the second one alone is not enough. A plan that produced no
    /// operations has no conflicts to show either — every request was dropped before it became one,
    /// or there were no requests — so "no conflicts" reads as ready when it means empty. SafeFileRenamer
    /// begins by refusing a plan that cannot execute, and RenamePreviewViewModel keeps its execute
    /// button on the same answer.
    /// </summary>
    [Fact]
    public void A_plan_with_nothing_to_do_cannot_execute_although_it_has_no_conflicts()
    {
        var empty = new RenamePlan(Guid.NewGuid(), Root, [], []);
        var ready = new RenamePlan(
            Guid.NewGuid(),
            Root,
            [new RenameOperation(0, Path.Combine(Root, "a.mkv"), Path.Combine(Root, "b.mkv"))],
            []);

        Assert.Empty(empty.Conflicts);
        Assert.False(empty.CanExecute);
        Assert.True(ready.CanExecute);
    }

    /// <summary>
    /// The defaults are contract, not convenience. SafeFileRenamer selects the operations it may undo
    /// by Status == Completed and refuses the whole undo unless CanUndo, so a record that arrived
    /// Completed or a plan that arrived undoable would offer to move files that were never moved. A
    /// conflict carries the index of the request that caused it because the plan does not otherwise
    /// say which of the caller's requests failed.
    /// </summary>
    [Fact]
    public void A_fresh_operation_is_planned_and_a_fresh_plan_cannot_be_undone()
    {
        var operation = new RenameOperation(7, Path.Combine(Root, "a.mkv"), Path.Combine(Root, "b.mkv"));
        var failed = operation with { Status = RenameOperationStatus.Failed, Error = "denied" };
        var id = Guid.NewGuid();
        var plan = new RenamePlan(id, Root, [operation], [new RenameConflict(3, RenameConflictKind.NoChange, "b.mkv")]);

        Assert.Equal(RenameOperationStatus.Planned, operation.Status);
        Assert.Null(operation.Error);
        Assert.Equal(7, operation.Sequence);
        Assert.Equal(RenameOperationStatus.Failed, failed.Status);
        Assert.Equal("denied", failed.Error);
        Assert.False(plan.CanUndo);
        Assert.Equal(id, plan.Id);
        Assert.Equal(Root, plan.RootPath);
        Assert.Equal(3, plan.Conflicts[0].RequestIndex);
    }
}
