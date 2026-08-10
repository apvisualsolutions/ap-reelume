// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Windows.Input;

namespace ApSolutions.LocalMedia.Presentation.Recovery;

public enum DatabaseRecoveryAction
{
    OpenBackupFolder,
    Exit,
}

public sealed class DatabaseRecoveryViewModel
{
    private static readonly IReadOnlyList<DatabaseRecoveryAction> SafeActions =
        [DatabaseRecoveryAction.OpenBackupFolder, DatabaseRecoveryAction.Exit];
    private readonly IReadOnlyList<DatabaseRecoveryAction> _availableActions;
    private readonly bool _canOverwriteBackup;

    public DatabaseRecoveryViewModel(string databasePath, string backupPath, string failureDetail)
        : this(databasePath, backupPath, failureDetail, actionHandler: null)
    {
    }

    public DatabaseRecoveryViewModel(
        string databasePath,
        string backupPath,
        string failureDetail,
        Action<DatabaseRecoveryAction>? actionHandler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureDetail);
        DatabasePath = databasePath;
        BackupPath = backupPath;
        FailureDetail = failureDetail;
        _availableActions = SafeActions;
        _canOverwriteBackup = false;
        RecoveryCommand = new SafeRecoveryCommand(actionHandler);
    }

    public string DatabasePath { get; }

    public string BackupPath { get; }

    public string FailureDetail { get; }

    public bool CanOverwriteBackup => _canOverwriteBackup;

    public IReadOnlyList<DatabaseRecoveryAction> AvailableActions => _availableActions;

    public ICommand RecoveryCommand { get; }

    private sealed class SafeRecoveryCommand(Action<DatabaseRecoveryAction>? actionHandler) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            parameter is DatabaseRecoveryAction action && SafeActions.Contains(action);

        public void Execute(object? parameter)
        {
            if (parameter is DatabaseRecoveryAction action && SafeActions.Contains(action))
            {
                actionHandler?.Invoke(action);
            }
        }
    }
}
