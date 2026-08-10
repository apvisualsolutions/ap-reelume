// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using ApSolutions.LocalMedia.Presentation.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

public sealed class RenameTransactionTests
{
    [Fact]
    public async Task Preview_has_no_io_and_conflicts_or_missing_consent_execute_zero_operations()
    {
        using var directory = new DatabaseTestDirectory();
        var policy = new RenamePolicy();
        var preview = new PreviewRename(policy);
        var recording = new RecordingRenamer();
        var execute = new ExecuteRename(recording);
        var valid = preview.Execute(new PreviewRenameCommand(directory.Path, [
            new RenameRequest(Path.Combine(directory.Path, "arrival.mkv"), "Arrival (2016).mkv"),
        ]));

        Assert.True(valid.CanExecute);
        Assert.Equal(0, recording.ExecuteCalls);

        var notConfirmed = await execute.ExecuteAsync(
            new ExecuteRenameCommand(valid, Confirmed: false),
            TestContext.Current.CancellationToken);
        var conflicted = preview.Execute(new PreviewRenameCommand(directory.Path, [
            new RenameRequest(Path.Combine(directory.Path, "one.mkv"), "same.mkv"),
            new RenameRequest(Path.Combine(directory.Path, "two.mkv"), "SAME.mkv"),
        ]));
        var blocked = await execute.ExecuteAsync(
            new ExecuteRenameCommand(conflicted, Confirmed: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.NotConfirmed, notConfirmed.Outcome);
        Assert.Equal(RenameExecutionOutcome.BlockedByConflict, blocked.Outcome);
        Assert.Equal(0, recording.ExecuteCalls);

        var undoNotConfirmed = await new UndoRename(recording).ExecuteAsync(
            new UndoRenameCommand(valid with { CanUndo = true }, Confirmed: false),
            TestContext.Current.CancellationToken);
        Assert.Equal(RenameExecutionOutcome.NotConfirmed, undoNotConfirmed.Outcome);
        Assert.Equal(0, recording.UndoCalls);
    }

    [Fact]
    public async Task Confirmed_local_batch_is_audited_per_item_and_undo_preserves_inventory()
    {
        using var directory = new DatabaseTestDirectory();
        var firstSource = Path.Combine(directory.Path, "arrival.2016.mkv");
        var secondSource = Path.Combine(directory.Path, "show.s01e02.mkv");
        await File.WriteAllTextAsync(firstSource, "movie-content", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(secondSource, "episode-content", TestContext.Current.CancellationToken);
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var adapter = new SafeFileRenamer(factory);
        var plan = new PreviewRename(new RenamePolicy()).Execute(new PreviewRenameCommand(directory.Path, [
            new RenameRequest(firstSource, "Arrival (2016).mkv"),
            new RenameRequest(secondSource, "Show - S01E02.mkv"),
        ]));

        var executed = await new ExecuteRename(adapter).ExecuteAsync(
            new ExecuteRenameCommand(plan, Confirmed: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.Succeeded, executed.Outcome);
        Assert.All(executed.Plan.Operations, operation => Assert.True(File.Exists(operation.DestinationPath)));
        Assert.All(executed.Plan.Operations, operation => Assert.False(File.Exists(operation.SourcePath)));
        Assert.Equal(["episode-content", "movie-content"], ReadInventory(directory.Path));
        var executionAudit = await adapter.GetAuditLogAsync(plan.Id, TestContext.Current.CancellationToken);
        Assert.Equal(2, executionAudit.Count);
        Assert.All(executionAudit, entry =>
        {
            Assert.Equal(RenameAuditDirection.Execute, entry.Direction);
            Assert.Equal(RenameAuditStatus.Completed, entry.Status);
        });

        var undone = await new UndoRename(adapter).ExecuteAsync(
            new UndoRenameCommand(executed.Plan, Confirmed: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.Undone, undone.Outcome);
        Assert.True(File.Exists(firstSource));
        Assert.True(File.Exists(secondSource));
        Assert.Equal(["episode-content", "movie-content"], ReadInventory(directory.Path));
        var completeAudit = await adapter.GetAuditLogAsync(plan.Id, TestContext.Current.CancellationToken);
        Assert.Equal(4, completeAudit.Count);
        Assert.Equal(2, completeAudit.Count(entry => entry.Direction == RenameAuditDirection.Undo));
    }

    /// <summary>
    /// WIN-004: a rename that hits a file another program holds open used to write "IOException"
    /// into the audit and say nothing on screen. The audit now names the situation, and the
    /// preview surface tells the person what to do about it — with a real file held by a real
    /// handle, the way a player or an indexer holds one.
    /// </summary>
    [Fact]
    public async Task A_rename_blocked_by_an_open_file_says_so_and_says_what_to_do()
    {
        using var directory = new DatabaseTestDirectory();
        var source = Path.Combine(directory.Path, "arrival.2016.mkv");
        await File.WriteAllTextAsync(source, "movie-content", TestContext.Current.CancellationToken);
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var adapter = new SafeFileRenamer(factory);
        var plan = new PreviewRename(new RenamePolicy()).Execute(new PreviewRenameCommand(directory.Path, [
            new RenameRequest(source, "Arrival (2016).mkv"),
        ]));
        var viewModel = new RenamePreviewViewModel(plan, new ExecuteRename(adapter), new UndoRename(adapter));

        using (new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            viewModel.IsConfirmed = true;
            viewModel.ExecuteCommand.Execute(null);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (viewModel.LastOutcome is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }

        Assert.Equal(RenameExecutionOutcome.Failed, viewModel.LastOutcome);
        Assert.True(viewModel.HasFailure);
        Assert.Equal("RenameFailedFileInUse", viewModel.FailureKey);
        Assert.True(File.Exists(source), "A refused rename moved the file anyway.");
        var audit = await adapter.GetAuditLogAsync(plan.Id, TestContext.Current.CancellationToken);
        var entry = Assert.Single(audit);
        Assert.Equal(RenameAuditStatus.Failed, entry.Status);
        Assert.Equal("FileInUse", entry.Error);
    }

    [Fact]
    public async Task Simulated_unc_failure_after_one_move_keeps_a_recoverable_log_and_inventory()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var root = @"\\server\share\Media";
        var firstSource = Path.Combine(root, "one.mkv");
        var secondSource = Path.Combine(root, "two.mkv");
        var fileSystem = new SimulatedRenameFileSystem(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [firstSource] = "first-content",
                [secondSource] = "second-content",
            },
            failOnMove: 2);
        var adapter = new SafeFileRenamer(factory, fileSystem);
        var plan = new RenamePolicy().CreatePlan(root, [
            new RenameRequest(firstSource, "One (2020).mkv"),
            new RenameRequest(secondSource, "Two (2021).mkv"),
        ]);

        var executed = await new ExecuteRename(adapter).ExecuteAsync(
            new ExecuteRenameCommand(plan, Confirmed: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.PartiallyCompleted, executed.Outcome);
        Assert.True(executed.Plan.CanUndo);
        Assert.Equal(["first-content", "second-content"], fileSystem.Inventory);
        var audit = await adapter.GetAuditLogAsync(plan.Id, TestContext.Current.CancellationToken);
        Assert.Equal([RenameAuditStatus.Completed, RenameAuditStatus.Failed], audit.Select(entry => entry.Status));

        var recovered = await new UndoRename(adapter).ExecuteAsync(
            new UndoRenameCommand(executed.Plan, Confirmed: true),
            TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.Undone, recovered.Outcome);
        Assert.True(fileSystem.FileExists(firstSource));
        Assert.True(fileSystem.FileExists(secondSource));
        Assert.Equal(["first-content", "second-content"], fileSystem.Inventory);
    }

    [Fact]
    public async Task Destination_created_after_preview_blocks_the_entire_real_batch_without_audit_or_io()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var source = Path.Combine(directory.Path, "source.mkv");
        var destination = Path.Combine(directory.Path, "Destination.mkv");
        await File.WriteAllTextAsync(source, "source-content", TestContext.Current.CancellationToken);
        var plan = new RenamePolicy().CreatePlan(directory.Path, [
            new RenameRequest(source, Path.GetFileName(destination)),
        ]);
        await File.WriteAllTextAsync(destination, "existing-content", TestContext.Current.CancellationToken);
        var adapter = new SafeFileRenamer(factory);

        var result = await adapter.ExecuteAsync(plan, TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.BlockedByConflict, result.Outcome);
        Assert.Contains(result.Plan.Conflicts, conflict => conflict.Kind == RenameConflictKind.DestinationExists);
        Assert.Equal("source-content", await File.ReadAllTextAsync(source, TestContext.Current.CancellationToken));
        Assert.Equal("existing-content", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
        Assert.Empty(await adapter.GetAuditLogAsync(plan.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Case_only_rename_uses_a_safe_intermediate_and_can_be_undone()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var source = Path.Combine(directory.Path, "arrival.mkv");
        await File.WriteAllTextAsync(source, "content", TestContext.Current.CancellationToken);
        var plan = new RenamePolicy().CreatePlan(directory.Path, [
            new RenameRequest(source, "ARRIVAL.mkv"),
        ]);
        var adapter = new SafeFileRenamer(factory);

        var executed = await adapter.ExecuteAsync(plan, TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.Succeeded, executed.Outcome);
        Assert.Equal("ARRIVAL.mkv", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(directory.Path, "*.mkv"))));

        var undone = await adapter.UndoAsync(executed.Plan, TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.Undone, undone.Outcome);
        Assert.Equal("arrival.mkv", Path.GetFileName(Assert.Single(Directory.EnumerateFiles(directory.Path, "*.mkv"))));
    }

    [Fact]
    public async Task Undo_refuses_changed_file_state_and_preview_view_model_requires_confirmation()
    {
        using var directory = new DatabaseTestDirectory();
        using var factory = new SqliteConnectionFactory(directory.DatabasePath);
        using var runner = new MigrationRunner(factory);
        await runner.MigrateAsync(TestContext.Current.CancellationToken);
        var source = Path.Combine(directory.Path, "movie.mkv");
        await File.WriteAllTextAsync(source, "content", TestContext.Current.CancellationToken);
        var adapter = new SafeFileRenamer(factory);
        var plan = new RenamePolicy().CreatePlan(directory.Path, [
            new RenameRequest(source, "Movie (2022).mkv"),
        ]);
        var executed = await adapter.ExecuteAsync(plan, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(source, "new conflicting content", TestContext.Current.CancellationToken);

        var unsafeUndo = await adapter.UndoAsync(executed.Plan, TestContext.Current.CancellationToken);

        Assert.Equal(RenameExecutionOutcome.UnsafeToUndo, unsafeUndo.Outcome);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(executed.Plan.Operations[0].DestinationPath));

        var recording = new RecordingRenamer();
        var viewModel = new RenamePreviewViewModel(plan, new ExecuteRename(recording), new UndoRename(recording));
        Assert.False(viewModel.ExecuteCommand.CanExecute(null));
        viewModel.IsConfirmed = true;
        Assert.True(viewModel.ExecuteCommand.CanExecute(null));
        viewModel.ExecuteCommand.Execute(null);
        Assert.Equal(1, recording.ExecuteCalls);
    }

    private static string[] ReadInventory(string root) =>
        Directory.EnumerateFiles(root, "*.mkv")
            .Select(File.ReadAllText)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private sealed class RecordingRenamer : ISafeFileRenamer
    {
        public int ExecuteCalls { get; private set; }

        public int UndoCalls { get; private set; }

        public Task<RenameExecutionResult> ExecuteAsync(RenamePlan plan, CancellationToken cancellationToken = default)
        {
            ExecuteCalls++;
            return Task.FromResult(new RenameExecutionResult(
                RenameExecutionOutcome.Succeeded,
                plan with { CanUndo = true }));
        }

        public Task<RenameExecutionResult> UndoAsync(RenamePlan plan, CancellationToken cancellationToken = default)
        {
            UndoCalls++;
            return Task.FromResult(new RenameExecutionResult(
                RenameExecutionOutcome.Undone,
                plan with { CanUndo = false }));
        }

        public Task<IReadOnlyList<RenameAuditEntry>> GetAuditLogAsync(
            Guid planId,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RenameAuditEntry>>([]);
    }

    private sealed class SimulatedRenameFileSystem(
        Dictionary<string, string> files,
        int failOnMove) : IRenameFileSystem
    {
        private int _moveCount;

        public string[] Inventory => files.Values.Order(StringComparer.Ordinal).ToArray();

        public bool FileExists(string path) => files.ContainsKey(path);

        public void MoveFile(string sourcePath, string destinationPath)
        {
            _moveCount++;
            if (_moveCount == failOnMove)
            {
                throw new IOException("Simulated UNC interruption.");
            }

            var content = files[sourcePath];
            files.Remove(sourcePath);
            files.Add(destinationPath, content);
        }
    }
}
