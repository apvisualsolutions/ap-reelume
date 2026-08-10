// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Identification;
using ApSolutions.LocalMedia.Domain.Identification;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Library;

namespace ApSolutions.LocalMedia.Presentation.Review;

/// <summary>
/// One candidate row a held reassignment may really be. The command carries the confirmation the
/// person is making: this file is the cataloged one, moved.
/// </summary>
public sealed class ReassignmentCandidateViewModel
{
    private readonly Func<ReassignmentCandidate, Task> _confirm;

    public ReassignmentCandidateViewModel(
        ReassignmentCandidate candidate,
        Func<ReassignmentCandidate, Task> confirm)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        _confirm = confirm ?? throw new ArgumentNullException(nameof(confirm));
        ConfirmCommand = new AsyncRelayCommand(() => _confirm(Candidate));
    }

    public ReassignmentCandidate Candidate { get; }

    public string Path => Candidate.Path;

    public ICommand ConfirmCommand { get; }
}

/// <summary>
/// A file a scan discovered that matches cataloged content without the certainty to act alone
/// (LIB-002/003). Confirming a candidate keeps the old entity — progress and decisions included —
/// under the new path; keeping it as new lets the file be its own entry from here on.
/// </summary>
public sealed class PendingReassignmentViewModel
{
    public PendingReassignmentViewModel(
        PendingReassignment pending,
        Func<ReassignmentCandidate, Task> confirm,
        Func<Task> keepAsNew)
    {
        Pending = pending ?? throw new ArgumentNullException(nameof(pending));
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(keepAsNew);
        Candidates = [.. pending.Candidates.Select(candidate =>
            new ReassignmentCandidateViewModel(candidate, confirm))];
        KeepAsNewCommand = new AsyncRelayCommand(keepAsNew);
    }

    public PendingReassignment Pending { get; }

    public string NewPath => Pending.Command.NewPath;

    public IReadOnlyList<ReassignmentCandidateViewModel> Candidates { get; }

    public ICommand KeepAsNewCommand { get; }
}

public sealed class CandidateCardViewModel(MatchCandidate candidate)
{
    public MatchCandidate Candidate { get; } = candidate ?? throw new ArgumentNullException(nameof(candidate));

    public string StableKey => Candidate.StableKey;

    public string ScorePercent => Candidate.Score.ToString("P0", CultureInfo.CurrentCulture);

    public ReviewState ReviewState => Candidate.ReviewState;

    public bool IsPending => ReviewState == ReviewState.Pending;

    public bool IsSuggested => ReviewState == ReviewState.Suggested;

    public IReadOnlyList<string> ExplanationCodes => Candidate.ExplanationCodes;

    public string ExplanationSummary => string.Join(", ", Candidate.ExplanationCodes);
}

public sealed class ReviewInboxViewModel : INotifyPropertyChanged
{
    private const int PageSize = 25;
    private readonly GetReviewInbox _getReviewInbox;
    private readonly ResolveMatch _resolveMatch;
    private readonly RejectMatch _rejectMatch;
    private readonly PendingReassignments? _reassignmentQueue;
    private readonly ManualReassignmentViewModel? _reassignment;
    private readonly ReconcileScannedFiles? _reconciliation;
    private IReadOnlyList<CandidateCardViewModel> _items = [];
    private IReadOnlyList<PendingReassignmentViewModel> _reassignments = [];
    private CandidateCardViewModel? _selectedItem;
    private string? _manualSearch;
    private int? _nextOffset;
    private bool _hasConflict;

    public ReviewInboxViewModel(
        GetReviewInbox getReviewInbox,
        ResolveMatch resolveMatch,
        RejectMatch rejectMatch,
        PendingReassignments? reassignmentQueue = null,
        ManualReassignmentViewModel? reassignment = null,
        ReconcileScannedFiles? reconciliation = null)
    {
        _getReviewInbox = getReviewInbox ?? throw new ArgumentNullException(nameof(getReviewInbox));
        _resolveMatch = resolveMatch ?? throw new ArgumentNullException(nameof(resolveMatch));
        _rejectMatch = rejectMatch ?? throw new ArgumentNullException(nameof(rejectMatch));
        _reassignmentQueue = reassignmentQueue;
        _reassignment = reassignment;
        _reconciliation = reconciliation;
        LoadMoreCommand = new AsyncRelayCommand(() => LoadMoreAsync(CancellationToken.None));
        AcceptSelectedCommand = new AsyncRelayCommand(() => AcceptSelectedAsync(CancellationToken.None));
        RejectSelectedCommand = new AsyncRelayCommand(() => RejectSelectedAsync(CancellationToken.None));
        SearchManuallyCommand = new RelayCommand(
            _ => RequestManualSearch(),
            _ => !string.IsNullOrWhiteSpace(ManualSearch));
        ClearSelectionCommand = new RelayCommand(_ => SelectedItem = null, _ => SelectedItem is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<string>? ManualSearchRequested;

    public IReadOnlyList<CandidateCardViewModel> Items
    {
        get => _items;
        private set
        {
            if (SetField(ref _items, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public CandidateCardViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    public string? ManualSearch
    {
        get => _manualSearch;
        set => SetField(ref _manualSearch, value);
    }

    public bool HasConflict
    {
        get => _hasConflict;
        private set => SetField(ref _hasConflict, value);
    }

    public bool IsEmpty => Items.Count == 0;

    public bool HasMore => _nextOffset.HasValue;

    /// <summary>The moved-file offers a person decides here, refreshed with every load.</summary>
    public IReadOnlyList<PendingReassignmentViewModel> Reassignments
    {
        get => _reassignments;
        private set
        {
            _reassignments = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReassignments));
        }
    }

    public bool HasReassignments => Reassignments.Count > 0;

    public ICommand LoadMoreCommand { get; }

    public ICommand AcceptSelectedCommand { get; }

    public ICommand RejectSelectedCommand { get; }

    public ICommand SearchManuallyCommand { get; }

    public ICommand ClearSelectionCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var page = await _getReviewInbox.ExecuteAsync(
            new GetReviewInboxQuery(PageSize),
            cancellationToken).ConfigureAwait(false);
        Items = page.Items.Select(candidate => new CandidateCardViewModel(candidate)).ToArray();
        _nextOffset = page.NextOffset;
        OnPropertyChanged(nameof(HasMore));
        ReloadReassignments();
    }

    private void ReloadReassignments()
    {
        if (_reassignmentQueue is null)
        {
            Reassignments = [];
            return;
        }

        Reassignments = [.. _reassignmentQueue.List().Select(pending => new PendingReassignmentViewModel(
            pending,
            candidate => ConfirmReassignmentAsync(pending, candidate, CancellationToken.None),
            () => KeepAsNewAsync(pending, CancellationToken.None)))];
    }

    /// <summary>
    /// The person said "this is the same file, moved": the old entity takes the new path through
    /// the confirmation flow, progress and decisions intact, and the offer leaves the inbox.
    /// </summary>
    public async Task ConfirmReassignmentAsync(
        PendingReassignment pending,
        ReassignmentCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(candidate);
        if (_reassignment is null || _reassignmentQueue is null)
        {
            return;
        }

        _reassignment.Review(pending.Command, candidate.MediaFileId);
        _ = await _reassignment.ConfirmAsync(cancellationToken).ConfigureAwait(true);
        _reassignmentQueue.Remove(pending.Command.NewPath);
        ReloadReassignments();
    }

    /// <summary>The person said "this is a new file": it keeps its own entry and stops being offered.</summary>
    public async Task KeepAsNewAsync(
        PendingReassignment pending,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pending);
        if (_reconciliation is null)
        {
            return;
        }

        await _reconciliation.KeepAsNewAsync(pending, cancellationToken).ConfigureAwait(true);
        ReloadReassignments();
    }

    public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
    {
        if (!_nextOffset.HasValue)
        {
            return;
        }

        var page = await _getReviewInbox.ExecuteAsync(
            new GetReviewInboxQuery(PageSize, _nextOffset.Value),
            cancellationToken).ConfigureAwait(false);
        Items = Items.Concat(page.Items.Select(candidate => new CandidateCardViewModel(candidate))).ToArray();
        _nextOffset = page.NextOffset;
        OnPropertyChanged(nameof(HasMore));
    }

    public async Task AcceptSelectedAsync(CancellationToken cancellationToken = default)
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return;
        }

        var candidate = selected.Candidate;
        var result = await _resolveMatch.ExecuteAsync(
            new ResolveMatchCommand(candidate.MediaFileId, candidate.Id, candidate.Revision),
            cancellationToken).ConfigureAwait(false);
        ApplyDecisionResult(selected, result);
    }

    public async Task RejectSelectedAsync(CancellationToken cancellationToken = default)
    {
        var selected = SelectedItem;
        if (selected is null)
        {
            return;
        }

        var candidate = selected.Candidate;
        var result = await _rejectMatch.ExecuteAsync(
            new RejectMatchCommand(candidate.MediaFileId, candidate.Id, candidate.Revision),
            cancellationToken).ConfigureAwait(false);
        ApplyDecisionResult(selected, result);
    }

    private void ApplyDecisionResult(CandidateCardViewModel selected, ReviewDecisionResult result)
    {
        HasConflict = result.Outcome == ReviewDecisionOutcome.Conflict;
        if (result.Outcome == ReviewDecisionOutcome.Applied)
        {
            Items = Items.Where(item => item != selected).ToArray();
            SelectedItem = null;
        }
        else if (result.Candidate is not null)
        {
            Items = Items.Select(item => item == selected ? new CandidateCardViewModel(result.Candidate) : item).ToArray();
            SelectedItem = Items.FirstOrDefault(item => item.Candidate.Id == result.Candidate.Id);
        }
    }

    private void RequestManualSearch()
    {
        if (!string.IsNullOrWhiteSpace(ManualSearch))
        {
            ManualSearchRequested?.Invoke(this, ManualSearch.Trim());
        }
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

    private sealed class RelayCommand(Action<object?> execute, Predicate<object?> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => canExecute(parameter);

        public void Execute(object? parameter) => execute(parameter);
    }
}
