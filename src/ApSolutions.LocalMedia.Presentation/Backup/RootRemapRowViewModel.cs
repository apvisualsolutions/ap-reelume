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
            OnPropertyChanged(nameof(NeedsFolder));
            OnPropertyChanged(nameof(StatusKey));
        }
    }

    /// <summary>True once this row asks for a folder other than the one the backup recorded.</summary>
    public bool HasRemap => !string.Equals(OldPath, NewPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether this row is asking somebody for a folder.
    /// </summary>
    /// <remarks>
    /// Every row used to carry a text box, so a restore of five roots offered five invitations to
    /// change something and four of them answered a question nobody had asked. A root that is where
    /// the backup left it does not need one. A missing folder does - the domain calls it a fact rather
    /// than a mistake - and so does a conflict, which is the only status that stops a restore. And a
    /// row somebody has already retyped keeps its box, so a wrong answer can be corrected.
    /// </remarks>
    public bool NeedsFolder => _status is RootRemapStatus.Missing or RootRemapStatus.Conflict || HasRemap;

    /// <summary>
    /// What the row says it is, taking a folder somebody has just typed into account.
    /// </summary>
    /// <remarks>
    /// The status comes from the last dry run, so a row went on saying the folder was missing while
    /// somebody was looking at the folder they had just typed in. A conflict is not covered up this
    /// way: it is the one status that blocks the restore, and it stays what it is until a run says
    /// otherwise.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// Four arms and every one of them reachable, which the first attempt at this was not: a ternary
    /// in front of the old switch left its <c>Remapped</c> arm dead, because a remapped root is by
    /// definition one whose new path differs. A branch nothing can take is the shape this repository
    /// has now removed four times in one batch.
    /// </para>
    /// <para>
    /// <c>Unchanged</c> needs no guard: its box never appears, so nobody can type into it.
    /// </para>
    /// </remarks>
    public string StatusKey => _status switch
    {
        RootRemapStatus.Conflict => "RestoreRootConflict",
        RootRemapStatus.Unchanged => "RestoreRootUnchanged",
        RootRemapStatus.Missing when !HasRemap => "RestoreRootMissing",
        _ => "RestoreRootRemapped",
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
        OnPropertyChanged(nameof(NeedsFolder));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
