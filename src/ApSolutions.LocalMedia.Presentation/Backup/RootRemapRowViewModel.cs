// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Domain.Backup;

namespace ApSolutions.LocalMedia.Presentation.Backup;

/// <summary>
/// One library root as the wizard shows it: where the backup thought it was, where it will be, and what
/// that means. The new folder is the only editable thing on the whole screen.
/// </summary>
public sealed class RootRemapRowViewModel : INotifyPropertyChanged
{
    private string _newPath;
    private RootRemapStatus _status;

    public RootRemapRowViewModel(RootRemapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        OldPath = decision.OldPath;
        _newPath = decision.NewPath;
        _status = decision.Status;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string OldPath { get; }

    public string NewPath
    {
        get => _newPath;
        set
        {
            if (string.Equals(_newPath, value, StringComparison.Ordinal))
            {
                return;
            }

            _newPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasRemap));
        }
    }

    /// <summary>True once this row asks for a folder other than the one the backup recorded.</summary>
    public bool HasRemap => !string.Equals(OldPath, NewPath, StringComparison.OrdinalIgnoreCase);

    public string StatusKey => _status switch
    {
        RootRemapStatus.Unchanged => "RestoreRootUnchanged",
        RootRemapStatus.Remapped => "RestoreRootRemapped",
        RootRemapStatus.Missing => "RestoreRootMissing",
        _ => "RestoreRootConflict",
    };

    public bool IsBlocking => _status == RootRemapStatus.Conflict;

    /// <summary>Takes the answer a fresh dry run gave for this same root.</summary>
    public void Apply(RootRemapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        _status = decision.Status;
        NewPath = decision.NewPath;
        OnPropertyChanged(nameof(StatusKey));
        OnPropertyChanged(nameof(IsBlocking));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
