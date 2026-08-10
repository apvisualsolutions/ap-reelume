// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Presentation.Metadata;

public sealed class RenamePreviewViewModel : INotifyPropertyChanged
{
    private readonly ExecuteRename _executeRename;
    private readonly UndoRename _undoRename;
    private RenamePlan _plan;
    private bool _isConfirmed;
    private RenameExecutionOutcome? _lastOutcome;

    public RenamePreviewViewModel(
        RenamePlan plan,
        ExecuteRename executeRename,
        UndoRename undoRename)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _executeRename = executeRename ?? throw new ArgumentNullException(nameof(executeRename));
        _undoRename = undoRename ?? throw new ArgumentNullException(nameof(undoRename));
        ExecuteCommand = new AsyncCommand(ExecuteAsync, () => IsConfirmed && Plan.CanExecute);
        UndoCommand = new AsyncCommand(UndoAsync, () => IsConfirmed && Plan.CanUndo);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RenamePlan Plan
    {
        get => _plan;
        private set
        {
            if (SetField(ref _plan, value))
            {
                OnPropertyChanged(nameof(Operations));
                OnPropertyChanged(nameof(Conflicts));
                OnPropertyChanged(nameof(HasConflicts));
                RaiseCommandStates();
            }
        }
    }

    public IReadOnlyList<RenameOperation> Operations => Plan.Operations;

    public IReadOnlyList<RenameConflict> Conflicts => Plan.Conflicts;

    public bool HasConflicts => Conflicts.Count > 0;

    public bool IsConfirmed
    {
        get => _isConfirmed;
        set
        {
            if (SetField(ref _isConfirmed, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public RenameExecutionOutcome? LastOutcome
    {
        get => _lastOutcome;
        private set => SetField(ref _lastOutcome, value);
    }

    /// <summary>
    /// The resource key for what went wrong and what to do about it, or null while nothing has.
    /// The audit log kept the failure and the screen said nothing actionable (WIN-004): a locked
    /// file asks for a different action than a folder this user cannot write into.
    /// </summary>
    public string? FailureKey =>
        LastOutcome is RenameExecutionOutcome.Failed or RenameExecutionOutcome.PartiallyCompleted
            ? Operations.FirstOrDefault(operation => operation.Status == RenameOperationStatus.Failed)?.Error switch
            {
                "FileInUse" => "RenameFailedFileInUse",
                "AccessDenied" => "RenameFailedAccessDenied",
                _ => "RenameFailedIo",
            }
            : null;

    public bool HasFailure => FailureKey is not null;

    public ICommand ExecuteCommand { get; }

    public ICommand UndoCommand { get; }

    private async Task ExecuteAsync()
    {
        var result = await _executeRename.ExecuteAsync(new ExecuteRenameCommand(Plan, IsConfirmed))
            .ConfigureAwait(true);
        ApplyResult(result);
    }

    private async Task UndoAsync()
    {
        var result = await _undoRename.ExecuteAsync(new UndoRenameCommand(Plan, IsConfirmed))
            .ConfigureAwait(true);
        ApplyResult(result);
    }

    private void ApplyResult(RenameExecutionResult result)
    {
        Plan = result.Plan;
        LastOutcome = result.Outcome;
        IsConfirmed = false;
        OnPropertyChanged(nameof(FailureKey));
        OnPropertyChanged(nameof(HasFailure));
    }

    private void RaiseCommandStates()
    {
        ((AsyncCommand)ExecuteCommand).RaiseCanExecuteChanged();
        ((AsyncCommand)UndoCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
    {
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning && canExecute();

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isRunning = true;
            RaiseCanExecuteChanged();
            try
            {
                await execute().ConfigureAwait(true);
            }
            finally
            {
                _isRunning = false;
                RaiseCanExecuteChanged();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
