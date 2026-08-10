// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Offers the stored position before the media starts. The prompt only appears when the decision was
/// to resume, so a file that was opened and closed never asks a pointless question.
/// </summary>
public sealed class ResumePromptViewModel : INotifyPropertyChanged
{
    private readonly Func<ResumeChoice, TimeSpan, Task>? _onChosen;
    private readonly ResumeDecision _decision;
    private ResumeChoice? _chosen;

    public ResumePromptViewModel(ResumeDecision decision, Func<ResumeChoice, TimeSpan, Task>? onChosen = null)
    {
        _decision = decision ?? throw new ArgumentNullException(nameof(decision));
        _onChosen = onChosen;
        ResumeCommand = new AsyncRelayCommand(() => ChooseAsync(ResumeChoice.Resume));
        RestartCommand = new AsyncRelayCommand(() => ChooseAsync(ResumeChoice.Restart));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ResumeCommand { get; }

    public ICommand RestartCommand { get; }

    /// <summary>True only while there is progress worth offering and nothing has been chosen yet.</summary>
    public bool IsVisible => _decision.Choice == ResumeChoice.Resume && _chosen is null;

    /// <summary>The stored point, written as hours, minutes, and seconds.</summary>
    public string ResumePositionText =>
        _decision.Position.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    /// <summary>What the person picked, or null while the prompt is still open.</summary>
    public ResumeChoice? Chosen
    {
        get => _chosen;
        private set
        {
            _chosen = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    private async Task ChooseAsync(ResumeChoice choice)
    {
        // The prompt is answered once: a second activation of either button changes nothing.
        if (_chosen is not null)
        {
            return;
        }

        Chosen = choice;
        var position = choice == ResumeChoice.Resume ? _decision.Position : TimeSpan.Zero;
        if (_onChosen is { } handler)
        {
            await handler(choice, position).ConfigureAwait(true);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
