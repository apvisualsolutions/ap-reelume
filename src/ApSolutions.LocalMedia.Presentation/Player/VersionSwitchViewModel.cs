// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>What the person decided about carrying progress across.</summary>
public enum VersionSwitchChoice
{
    Confirm,
    Restart,
    Cancel,
}

/// <summary>
/// Asks before moving progress that cannot be carried across safely. It states the reason in words and
/// shows the second it would land on, so agreeing is an informed choice rather than a shrug.
/// </summary>
public sealed class VersionSwitchViewModel : INotifyPropertyChanged
{
    private readonly Func<VersionSwitchChoice, Task>? _onChosen;
    private ProgressTransferDecision? _decision;
    private VersionSwitchChoice? _chosen;

    public VersionSwitchViewModel(Func<VersionSwitchChoice, Task>? onChosen = null)
    {
        _onChosen = onChosen;
        ConfirmCommand = new ChoiceCommand(() => ChooseAsync(VersionSwitchChoice.Confirm));
        RestartCommand = new ChoiceCommand(() => ChooseAsync(VersionSwitchChoice.Restart));
        CancelCommand = new ChoiceCommand(() => ChooseAsync(VersionSwitchChoice.Cancel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ConfirmCommand { get; }

    public ICommand RestartCommand { get; }

    public ICommand CancelCommand { get; }

    /// <summary>Only a pending confirmation is worth showing.</summary>
    public bool IsVisible => _decision?.Kind == ProgressTransferKind.Confirm && _chosen is null;

    public bool IsLargeDifference => Reason == ProgressTransferReason.LargeDifference;

    public bool IsUnknownDuration => Reason == ProgressTransferReason.UnknownDuration;

    public bool IsIncompatibleStructure => Reason == ProgressTransferReason.IncompatibleStructure;

    /// <summary>The second the switch would land on, as hours, minutes, and seconds.</summary>
    public string SuggestedPositionText =>
        (_decision?.Position ?? TimeSpan.Zero).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    public VersionSwitchChoice? Chosen => _chosen;

    private ProgressTransferReason Reason =>
        IsPending ? _decision!.Reason : ProgressTransferReason.None;

    private bool IsPending => _decision?.Kind == ProgressTransferKind.Confirm;

    /// <summary>Shows the question the policy raised, or hides it when there is nothing to ask.</summary>
    public void Apply(ProgressTransferDecision decision)
    {
        _decision = decision ?? throw new ArgumentNullException(nameof(decision));
        _chosen = null;
        RaiseAll();
    }

    private async Task ChooseAsync(VersionSwitchChoice choice)
    {
        if (_chosen is not null)
        {
            return;
        }

        _chosen = choice;
        RaiseAll();
        if (_onChosen is { } handler)
        {
            await handler(choice).ConfigureAwait(true);
        }
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
        {
            nameof(IsVisible),
            nameof(IsLargeDifference),
            nameof(IsUnknownDuration),
            nameof(IsIncompatibleStructure),
            nameof(SuggestedPositionText),
            nameof(Chosen),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class ChoiceCommand(Func<Task> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public async void Execute(object? parameter)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            await execute().ConfigureAwait(true);
        }
    }
}
