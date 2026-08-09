using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

public sealed class RenamePolicyTests
{
    [Fact]
    public void Invalid_and_reserved_names_are_deterministic_while_overlong_names_are_blocked()
    {
        var root = Path.GetFullPath(@"C:\Media");
        var policy = new RenamePolicy();
        var requests = new[]
        {
            new RenameRequest(Path.Combine(root, "matrix.mkv"), "The <Matrix>: 1999?.mkv"),
            new RenameRequest(Path.Combine(root, "classic.mkv"), "CON.mkv"),
            new RenameRequest(Path.Combine(root, "long.mkv"), $"{new string('a', 256)}.mkv"),
        };

        var first = policy.CreatePlan(root, requests);
        var repeated = policy.CreatePlan(root, requests);

        Assert.Equal("The -Matrix-- 1999-.mkv", Path.GetFileName(first.Operations[0].DestinationPath));
        Assert.Equal("_CON.mkv", Path.GetFileName(first.Operations[1].DestinationPath));
        Assert.Equal(
            first.Operations.Select(operation => operation.DestinationPath),
            repeated.Operations.Select(operation => operation.DestinationPath));
        Assert.Contains(first.Conflicts, conflict => conflict.Kind == RenameConflictKind.PathTooLong);
        Assert.DoesNotContain(first.Operations, operation => operation.SourcePath.EndsWith("long.mkv", StringComparison.Ordinal));
    }

    [Fact]
    public void Outside_root_folder_moves_and_case_insensitive_batch_collisions_are_blocked()
    {
        var root = Path.GetFullPath(@"C:\Media");
        var policy = new RenamePolicy();

        var plan = policy.CreatePlan(root, [
            new RenameRequest(Path.GetFullPath(@"C:\Private\outside.mkv"), "outside.mkv"),
            new RenameRequest(Path.Combine(root, "escape.mkv"), @"..\escape.mkv"),
            new RenameRequest(Path.Combine(root, "nested.mkv"), @"sub\nested.mkv"),
            new RenameRequest(Path.Combine(root, "one.mkv"), "Same.mkv"),
            new RenameRequest(Path.Combine(root, "two.mkv"), "same.MKV"),
        ]);

        Assert.Contains(plan.Conflicts, conflict => conflict.Kind == RenameConflictKind.SourceOutsideRoot);
        Assert.Contains(plan.Conflicts, conflict => conflict.Kind == RenameConflictKind.DestinationOutsideRoot);
        Assert.Contains(plan.Conflicts, conflict => conflict.Kind == RenameConflictKind.FolderMove);
        Assert.Equal(2, plan.Conflicts.Count(conflict => conflict.Kind == RenameConflictKind.DuplicateDestination));
        Assert.False(plan.CanExecute);
    }

    [Fact]
    public void A_single_case_only_rename_is_allowed_but_same_path_is_not_an_operation()
    {
        var root = Path.GetFullPath(@"C:\Media");
        var policy = new RenamePolicy();

        var caseOnly = policy.CreatePlan(root, [
            new RenameRequest(Path.Combine(root, "arrival.mkv"), "ARRIVAL.mkv"),
        ]);
        var unchanged = policy.CreatePlan(root, [
            new RenameRequest(Path.Combine(root, "arrival.mkv"), "arrival.mkv"),
        ]);

        Assert.True(caseOnly.CanExecute);
        Assert.Single(caseOnly.Operations);
        Assert.True(caseOnly.Operations[0].IsCaseOnlyChange);
        Assert.Empty(unchanged.Operations);
        Assert.Contains(unchanged.Conflicts, conflict => conflict.Kind == RenameConflictKind.NoChange);
    }

    [Fact]
    public void Missing_malformed_empty_and_full_length_paths_are_reported_without_throwing()
    {
        var root = Path.GetFullPath(@"C:\Media");
        var longRoot = @"C:\";
        while (longRoot.Length < 32_520)
        {
            longRoot = Path.Combine(longRoot, new string('r', 200));
        }

        var policy = new RenamePolicy();
        var invalid = policy.CreatePlan(root, [
            null!,
            new RenameRequest("bad\0source.mkv", "bad.mkv"),
            new RenameRequest(Path.Combine(root, "empty.mkv"), " "),
            new RenameRequest(Path.Combine(root, "dot.mkv"), "."),
            new RenameRequest(Path.Combine(root, "rooted.mkv"), @"C:\Else\outside.mkv"),
            new RenameRequest(Path.Combine(root, "control.mkv"), "Line\u0001Break.mkv"),
        ]);
        var longPath = policy.CreatePlan(longRoot, [
            new RenameRequest(Path.Combine(longRoot, "source.mkv"), $"{new string('d', 250)}.mkv"),
        ]);

        Assert.Equal(3, invalid.Conflicts.Count(conflict => conflict.Kind == RenameConflictKind.InvalidPath));
        Assert.Contains(invalid.Conflicts, conflict => conflict.Kind == RenameConflictKind.FolderMove);
        Assert.Contains(invalid.Conflicts, conflict => conflict.Kind == RenameConflictKind.DestinationOutsideRoot);
        Assert.Contains(invalid.Operations, operation => Path.GetFileName(operation.DestinationPath) == "Line-Break.mkv");
        Assert.Contains(longPath.Conflicts, conflict => conflict.Kind == RenameConflictKind.PathTooLong);
    }
}
