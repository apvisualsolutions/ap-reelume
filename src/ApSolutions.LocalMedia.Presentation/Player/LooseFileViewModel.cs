// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Playback;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// The banner shown while a file that is not in the library is playing.
/// <para>
/// It offers exactly one way to change that, and it asks first: adding the containing folder is a
/// library decision, so it goes through the same confirmation the root flow uses. The single file is
/// never imported on its own.
/// </para>
/// </summary>
public sealed class LooseFileViewModel : INotifyPropertyChanged
{
    private readonly Func<string, Task>? _onAddFolder;
    private LooseFileSession? _session;
    private bool _isAddFolderConfirmationPending;

    public LooseFileViewModel(Func<string, Task>? onAddFolder = null)
    {
        _onAddFolder = onAddFolder;
        AddFolderCommand = new LooseCommand(RequestAddFolder, () => _session is not null);
        ConfirmAddFolderCommand = new LooseCommand(ConfirmAddFolder, () => true);
        CancelAddFolderCommand = new LooseCommand(CancelAddFolder, () => true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand AddFolderCommand { get; }

    public ICommand ConfirmAddFolderCommand { get; }

    public ICommand CancelAddFolderCommand { get; }

    /// <summary>True while something outside the library is playing.</summary>
    public bool IsLooseSession => _session is not null;

    /// <summary>The file name, never the whole path: a banner is not a place for someone's folders.</summary>
    public string DisplayName => _session?.DisplayName ?? string.Empty;

    public string FolderPath => _session?.FolderPath ?? string.Empty;

    public bool IsAddFolderConfirmationPending
    {
        get => _isAddFolderConfirmationPending;
        private set
        {
            if (_isAddFolderConfirmationPending == value)
            {
                return;
            }

            _isAddFolderConfirmationPending = value;
            OnPropertyChanged();
        }
    }

    public void Apply(LooseFileSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        IsAddFolderConfirmationPending = false;
        Refresh();
    }

    /// <summary>Puts the banner away when the loose session ends.</summary>
    public void Clear()
    {
        _session = null;
        IsAddFolderConfirmationPending = false;
        Refresh();
    }

    private void RequestAddFolder()
    {
        if (_session is null)
        {
            return;
        }

        IsAddFolderConfirmationPending = true;
    }

    private void ConfirmAddFolder()
    {
        if (!IsAddFolderConfirmationPending || _session is not { } session)
        {
            return;
        }

        IsAddFolderConfirmationPending = false;
        _ = _onAddFolder?.Invoke(session.FolderPath);
    }

    private void CancelAddFolder() => IsAddFolderConfirmationPending = false;

    private void Refresh()
    {
        OnPropertyChanged(nameof(IsLooseSession));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(FolderPath));
        (AddFolderCommand as LooseCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class LooseCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter)
        {
            if (canExecute())
            {
                execute();
            }
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
