using System.Globalization;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public interface IRenameFileSystem
{
    bool FileExists(string path);

    void MoveFile(string sourcePath, string destinationPath);
}

public sealed class SafeFileRenamer : ISafeFileRenamer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IRenameFileSystem _fileSystem;
    private readonly TimeProvider _timeProvider;

    public SafeFileRenamer(
        SqliteConnectionFactory connectionFactory,
        IRenameFileSystem? fileSystem = null,
        TimeProvider? timeProvider = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _fileSystem = fileSystem ?? new SystemRenameFileSystem();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<RenameExecutionResult> ExecuteAsync(
        RenamePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanExecute)
        {
            return new RenameExecutionResult(RenameExecutionOutcome.BlockedByConflict, plan);
        }

        var stateConflicts = ValidateExecutionState(plan);
        if (stateConflicts.Count > 0)
        {
            return new RenameExecutionResult(
                RenameExecutionOutcome.BlockedByConflict,
                plan with { Conflicts = [.. plan.Conflicts, .. stateConflicts] });
        }

        var operations = plan.Operations.ToArray();
        var completed = 0;
        var failed = false;
        for (var index = 0; index < operations.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operation = operations[index];
            var auditId = await CreateAuditAsync(
                plan.Id,
                operation,
                RenameAuditDirection.Execute,
                operation.SourcePath,
                operation.DestinationPath,
                cancellationToken).ConfigureAwait(false);
            try
            {
                Move(operation.SourcePath, operation.DestinationPath, operation.IsCaseOnlyChange);
                operations[index] = operation with { Status = RenameOperationStatus.Completed };
                completed++;
                await CompleteAuditAsync(auditId, RenameAuditStatus.Completed, null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                await RecordFailureAsync(auditId, operations, index, operation, exception, cancellationToken)
                    .ConfigureAwait(false);
                failed = true;
                break;
            }
            catch (UnauthorizedAccessException exception)
            {
                await RecordFailureAsync(auditId, operations, index, operation, exception, cancellationToken)
                    .ConfigureAwait(false);
                failed = true;
                break;
            }
        }

        var outcome = failed
            ? completed > 0 ? RenameExecutionOutcome.PartiallyCompleted : RenameExecutionOutcome.Failed
            : RenameExecutionOutcome.Succeeded;
        return new RenameExecutionResult(
            outcome,
            plan with { Operations = operations, CanUndo = completed > 0 });
    }

    public async Task<RenameExecutionResult> UndoAsync(
        RenamePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanUndo)
        {
            return new RenameExecutionResult(RenameExecutionOutcome.NothingToUndo, plan);
        }

        var completedIndexes = plan.Operations
            .Select((operation, index) => (operation, index))
            .Where(item => item.operation.Status == RenameOperationStatus.Completed)
            .Reverse()
            .ToArray();
        if (completedIndexes.Length == 0 || !CanUndoAll(plan.RootPath, completedIndexes.Select(item => item.operation)))
        {
            return new RenameExecutionResult(RenameExecutionOutcome.UnsafeToUndo, plan);
        }

        var operations = plan.Operations.ToArray();
        var failed = false;
        foreach (var (operation, index) in completedIndexes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var auditId = await CreateAuditAsync(
                plan.Id,
                operation,
                RenameAuditDirection.Undo,
                operation.DestinationPath,
                operation.SourcePath,
                cancellationToken).ConfigureAwait(false);
            try
            {
                Move(operation.DestinationPath, operation.SourcePath, operation.IsCaseOnlyChange);
                operations[index] = operation with { Status = RenameOperationStatus.Undone, Error = null };
                await CompleteAuditAsync(auditId, RenameAuditStatus.Completed, null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                await CompleteAuditAsync(
                    auditId,
                    RenameAuditStatus.Failed,
                    Classify(exception),
                    cancellationToken).ConfigureAwait(false);
                failed = true;
                break;
            }
            catch (UnauthorizedAccessException exception)
            {
                await CompleteAuditAsync(
                    auditId,
                    RenameAuditStatus.Failed,
                    Classify(exception),
                    cancellationToken).ConfigureAwait(false);
                failed = true;
                break;
            }
        }

        var canUndo = operations.Any(operation => operation.Status == RenameOperationStatus.Completed);
        return new RenameExecutionResult(
            failed ? RenameExecutionOutcome.PartiallyCompleted : RenameExecutionOutcome.Undone,
            plan with { Operations = operations, CanUndo = canUndo });
    }

    public async Task<IReadOnlyList<RenameAuditEntry>> GetAuditLogAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, operation_sequence, direction, source_path, destination_path,
                   status, occurred_at, error
            FROM rename_log
            WHERE plan_id = $planId
            ORDER BY id;
            """;
        command.Parameters.AddWithValue("$planId", planId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var entries = new List<RenameAuditEntry>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new RenameAuditEntry(
                reader.GetInt64(0),
                planId,
                reader.GetInt32(1),
                (RenameAuditDirection)reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                (RenameAuditStatus)reader.GetInt32(5),
                DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
        }

        return entries;
    }

    private List<RenameConflict> ValidateExecutionState(RenamePlan plan)
    {
        var conflicts = new List<RenameConflict>();
        foreach (var operation in plan.Operations)
        {
            if (!IsNormalizedAndSafe(operation, plan.RootPath))
            {
                conflicts.Add(new RenameConflict(
                    operation.Sequence,
                    RenameConflictKind.UnsafeState,
                    "Rename paths are not normalized and confined."));
            }
            else if (!_fileSystem.FileExists(operation.SourcePath))
            {
                conflicts.Add(new RenameConflict(
                    operation.Sequence,
                    RenameConflictKind.SourceMissing,
                    operation.SourcePath));
            }
            else if (!operation.IsCaseOnlyChange && _fileSystem.FileExists(operation.DestinationPath))
            {
                conflicts.Add(new RenameConflict(
                    operation.Sequence,
                    RenameConflictKind.DestinationExists,
                    operation.DestinationPath));
            }
        }

        return conflicts;
    }

    private bool CanUndoAll(string rootPath, IEnumerable<RenameOperation> operations)
    {
        foreach (var operation in operations)
        {
            if (!IsNormalizedAndSafe(operation, rootPath)
                || !_fileSystem.FileExists(operation.DestinationPath)
                || (!operation.IsCaseOnlyChange && _fileSystem.FileExists(operation.SourcePath)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNormalizedAndSafe(RenameOperation operation, string rootPath)
    {
        var normalizedSource = Path.GetFullPath(operation.SourcePath);
        var normalizedDestination = Path.GetFullPath(operation.DestinationPath);
        return string.Equals(normalizedSource, operation.SourcePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedDestination, operation.DestinationPath, StringComparison.OrdinalIgnoreCase)
            && RenamePolicy.IsConfinedToRoot(normalizedSource, rootPath)
            && RenamePolicy.IsConfinedToRoot(normalizedDestination, rootPath)
            && string.Equals(
                Path.GetDirectoryName(normalizedSource),
                Path.GetDirectoryName(normalizedDestination),
                StringComparison.OrdinalIgnoreCase);
    }

    private void Move(string sourcePath, string destinationPath, bool isCaseOnlyChange)
    {
        if (!isCaseOnlyChange)
        {
            _fileSystem.MoveFile(sourcePath, destinationPath);
            return;
        }

        var directory = Path.GetDirectoryName(sourcePath)!;
        string temporaryPath;
        do
        {
            temporaryPath = Path.Combine(directory, $".ap-reelume-{Guid.NewGuid():N}.tmp");
        }
        while (_fileSystem.FileExists(temporaryPath));

        _fileSystem.MoveFile(sourcePath, temporaryPath);
        try
        {
            _fileSystem.MoveFile(temporaryPath, destinationPath);
        }
        catch
        {
            if (_fileSystem.FileExists(temporaryPath) && !_fileSystem.FileExists(sourcePath))
            {
                _fileSystem.MoveFile(temporaryPath, sourcePath);
            }

            throw;
        }
    }

    private async Task<long> CreateAuditAsync(
        Guid planId,
        RenameOperation operation,
        RenameAuditDirection direction,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO rename_log (
                plan_id, operation_sequence, direction, source_path,
                destination_path, status, occurred_at, error)
            VALUES (
                $planId, $sequence, $direction, $source, $destination,
                $status, $occurredAt, NULL);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$planId", planId.ToString("D"));
        command.Parameters.AddWithValue("$sequence", operation.Sequence);
        command.Parameters.AddWithValue("$direction", (int)direction);
        command.Parameters.AddWithValue("$source", sourcePath);
        command.Parameters.AddWithValue("$destination", destinationPath);
        command.Parameters.AddWithValue("$status", (int)RenameAuditStatus.Started);
        command.Parameters.AddWithValue(
            "$occurredAt",
            _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
    }

    private async Task CompleteAuditAsync(
        long auditId,
        RenameAuditStatus status,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE rename_log
            SET status = $status,
                error = $error
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$status", (int)status);
        command.Parameters.AddWithValue("$error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", auditId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        long auditId,
        RenameOperation[] operations,
        int index,
        RenameOperation operation,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = Classify(exception);
        operations[index] = operation with { Status = RenameOperationStatus.Failed, Error = error };
        await CompleteAuditAsync(auditId, RenameAuditStatus.Failed, error, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Names the failure by what a person can do about it, not by the exception type. A file
    /// somebody is playing and a folder Windows will not let this user write into need different
    /// actions, and "IOException" in the audit demanded neither (WIN-004).
    /// </summary>
    private static string Classify(Exception exception) => exception switch
    {
        // ERROR_SHARING_VIOLATION (32) and ERROR_LOCK_VIOLATION (33), the two shapes "another
        // program has it open" takes on Windows.
        IOException io when (io.HResult & 0xFFFF) is 32 or 33 => "FileInUse",
        UnauthorizedAccessException => "AccessDenied",
        _ => exception.GetType().Name,
    };

    private sealed class SystemRenameFileSystem : IRenameFileSystem
    {
        public bool FileExists(string path) => File.Exists(path);

        public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);
    }
}
