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

public sealed class CandidateCardViewModel
{
    public CandidateCardViewModel(
        MatchCandidate candidate,
        Func<CandidateCardViewModel, Task>? onAccept = null,
        Func<CandidateCardViewModel, Task>? onReject = null,
        Action<CandidateCardViewModel>? onSearchManually = null)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        AcceptCommand = new AsyncRelayCommand(
            () => onAccept is null ? Task.CompletedTask : onAccept(this),
            () => onAccept is not null);
        RejectCommand = new AsyncRelayCommand(
            () => onReject is null ? Task.CompletedTask : onReject(this),
            () => onReject is not null);
        SearchManuallyCommand = new AsyncRelayCommand(
            () =>
            {
                onSearchManually?.Invoke(this);
                return Task.CompletedTask;
            },
            () => onSearchManually is not null);
    }

    public MatchCandidate Candidate { get; }

    /// <summary>
    /// Accepts, rejects, or starts a manual search for <b>this</b> file.
    /// </summary>
    /// <remarks>
    /// The decisions used to live one row below the list and act on whatever was selected, which is
    /// one decision per tray rather than one per file — and the prototype puts them in the card
    /// because that is where the file is. Each of them selects this card first, so the keyboard path
    /// and the mouse path end in the same place.
    /// </remarks>
    public ICommand AcceptCommand { get; }

    public ICommand RejectCommand { get; }

    public ICommand SearchManuallyCommand { get; }

    public string StableKey => Candidate.StableKey;

    /// <summary>The file's own name, which is what the tray is asking about.</summary>
    public string FileName => Candidate.MediaFilePath is { Length: > 0 } path
        ? System.IO.Path.GetFileName(path)
        : string.Empty;

    /// <summary>And the folder it sits in, which is what tells two files of the same name apart.</summary>
    public string FileFolder => Candidate.MediaFilePath is { Length: > 0 } path
        ? System.IO.Path.GetDirectoryName(path) ?? string.Empty
        : string.Empty;

    public bool HasFile => FileName.Length > 0;

    /// <summary>
    /// «Película» or «Serie», as a resource key: what kind of thing is being proposed. An episode
    /// candidate says «Serie», which is what the prototype writes and what a person is choosing —
    /// the episode itself is already named by the candidate above it.
    /// </summary>
    public string KindKey => Candidate.Kind == CandidateContentKind.Episode
        ? "CatalogKindShow"
        : "CatalogKindMovie";

    public string ScorePercent => Candidate.Score.ToString("P0", CultureInfo.CurrentCulture);

    public ReviewState ReviewState => Candidate.ReviewState;

    public bool IsPending => ReviewState == ReviewState.Pending;

    public bool IsSuggested => ReviewState == ReviewState.Suggested;

    /// <summary>
    /// The codes as codes. What the screen shows is their words, resolved by the converter the
    /// recommendation reasons already go through - a summary joined here would be one built out of
    /// code paths, which is exactly what the help text used to announce.
    /// </summary>
    public IReadOnlyList<string> ExplanationCodes => Candidate.ExplanationCodes;
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
    private readonly SearchForMatch? _manualSearch;

    // Held as what they are rather than looked up out of the ICommand properties with `as`. Those
    // properties are only ever these two objects, so the cast could not fail — but it could stop
    // matching, silently, and a command that quietly stops announcing CanExecuteChanged is exactly
    // the defect ARQ-004 left in this class: a button that asks once and never again.
    private readonly AsyncRelayCommand _searchManually;
    private readonly AsyncRelayCommand _clearSelection;
    private IReadOnlyList<CandidateCardViewModel> _items = [];
    private IReadOnlyList<PendingReassignmentViewModel> _reassignments = [];
    private CandidateCardViewModel? _selectedItem;
    private string? _manualSearchText;
    private int? _nextOffset;
    private bool _hasConflict;

    public ReviewInboxViewModel(
        GetReviewInbox getReviewInbox,
        ResolveMatch resolveMatch,
        RejectMatch rejectMatch,
        PendingReassignments? reassignmentQueue = null,
        ManualReassignmentViewModel? reassignment = null,
        ReconcileScannedFiles? reconciliation = null,
        SearchForMatch? manualSearch = null)
    {
        _getReviewInbox = getReviewInbox ?? throw new ArgumentNullException(nameof(getReviewInbox));
        _resolveMatch = resolveMatch ?? throw new ArgumentNullException(nameof(resolveMatch));
        _rejectMatch = rejectMatch ?? throw new ArgumentNullException(nameof(rejectMatch));
        _reassignmentQueue = reassignmentQueue;
        _reassignment = reassignment;
        _reconciliation = reconciliation;
        _manualSearch = manualSearch;
        LoadMoreCommand = new AsyncRelayCommand(() => LoadMoreAsync(CancellationToken.None));
        AcceptSelectedCommand = new AsyncRelayCommand(() => AcceptSelectedAsync(CancellationToken.None));
        RejectSelectedCommand = new AsyncRelayCommand(() => RejectSelectedAsync(CancellationToken.None));

        // Both of these answer a question that changes while the surface is on screen — is anything
        // typed, is anything selected — so both have to be able to say the answer changed. The class
        // that used to back them could not: its CanExecuteChanged had an empty add and remove, so a
        // button asked once, at construction, and never again. Typing into the search box left Search
        // disabled for good (ARQ-004 replaced twenty-four such classes; this pair was missed).
        _searchManually = new AsyncRelayCommand(
            () => SearchManuallyAsync(CancellationToken.None),
            CanSearchManually);
        _clearSelection = new AsyncRelayCommand(
            () =>
            {
                SelectedItem = null;
                return Task.CompletedTask;
            },
            () => SelectedItem is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
        set
        {
            if (SetField(ref _selectedItem, value))
            {
                RaiseSelectionDependentCommands();
            }
        }
    }

    /// <summary>
    /// The words a person types when none of the offers is right. What is searched for is the file
    /// the selected candidate belongs to, so both are required before the search can run.
    /// </summary>
    public string? ManualSearch
    {
        get => _manualSearchText;
        set
        {
            if (SetField(ref _manualSearchText, value))
            {
                _searchManually.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasConflict
    {
        get => _hasConflict;
        private set => SetField(ref _hasConflict, value);
    }

    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// «5 archivos esperando tu decisión», which is the line the prototype writes under the intro.
    /// </summary>
    /// <remarks>
    /// A tray with no count is a tray whose length you learn by scrolling to the end of it, and the
    /// number is the whole reason somebody opens this surface rather than the library.
    /// </remarks>
    public string CountText => Resource("ReviewInboxCount", "{0} files waiting for your decision")
        .Replace("{0}", Items.Count.ToString(CultureInfo.CurrentCulture), StringComparison.Ordinal);

    /// <summary>
    /// The one string this model assembles rather than picks. The fallback keeps a headless test —
    /// which mounts this without the dictionaries — printing a sentence rather than a blank.
    /// </summary>
    private static string Resource(string key, string fallback) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            && value is string text
                ? text
                : fallback;

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

    public ICommand SearchManuallyCommand => _searchManually;

    public ICommand ClearSelectionCommand => _clearSelection;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var page = await _getReviewInbox.ExecuteAsync(
            new GetReviewInboxQuery(PageSize),
            cancellationToken).ConfigureAwait(false);
        Items = page.Items.Select(Card).ToArray();
        _nextOffset = page.NextOffset;
        OnPropertyChanged(nameof(HasMore));
        OnPropertyChanged(nameof(CountText));
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
            Items = Items.Select(item => item == selected ? Card(result.Candidate) : item).ToArray();
            SelectedItem = Items.FirstOrDefault(item => item.Candidate.Id == result.Candidate.Id);
        }
    }

    /// <summary>
    /// Searches for what a person typed, for the file the selected candidate belongs to, and puts
    /// the answers where the wrong ones were.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The typed words go through the same reading a file name goes through: the parser separates
    /// title from year, the provider is asked, the scorer ranks what comes back, and the candidates
    /// replace the ones this file had. That is the whole of what "search manually" can mean here, and
    /// it needs no path of its own — an identification is an identification however the words arrived.
    /// </para>
    /// <para>
    /// Until this existed the button raised an event, and <b>nothing in the application listened to
    /// it</b>: the press was answered by a search that never happened. It is this repository's
    /// characteristic defect wearing an event instead of a registration.
    /// </para>
    /// </remarks>
    public async Task SearchManuallyAsync(CancellationToken cancellationToken = default)
    {
        if (!CanSearchManually())
        {
            return;
        }

        _ = await _manualSearch!.ExecuteAsync(
            SelectedItem!.Candidate.MediaFileId,
            ManualSearch!,
            cancellationToken).ConfigureAwait(true);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// A search needs both halves: words to search for, and the file they are about. Without a
    /// selected candidate there is no file, and a button that looked available would be a button that
    /// answers nothing.
    /// </summary>
    private bool CanSearchManually() =>
        _manualSearch is not null
        && SelectedItem is not null
        && !string.IsNullOrWhiteSpace(ManualSearch);

    /// <summary>
    /// One card, wired to the three things a person can do to the file behind it. Selecting first is
    /// not a detail: everything downstream — the decision, the manual search, the conflict message —
    /// is written in terms of the selected candidate, and a card that acted without selecting would
    /// be deciding about one file while the tray still pointed at another.
    /// </summary>
    private CandidateCardViewModel Card(MatchCandidate candidate)
    {
        CandidateCardViewModel? card = null;
        card = new CandidateCardViewModel(
            candidate,
            async _ =>
            {
                SelectedItem = card;
                await AcceptSelectedAsync(CancellationToken.None).ConfigureAwait(true);
            },
            async _ =>
            {
                SelectedItem = card;
                await RejectSelectedAsync(CancellationToken.None).ConfigureAwait(true);
            },
            _ =>
            {
                SelectedItem = card;

                // The words to search with default to the file's own name, which is what a person
                // would type first and what the parser already knows how to read.
                if (string.IsNullOrWhiteSpace(ManualSearch) && card?.FileName is { Length: > 0 } name)
                {
                    ManualSearch = System.IO.Path.GetFileNameWithoutExtension(name);
                }
            });
        return card;
    }

    private void RaiseSelectionDependentCommands()
    {
        _searchManually.RaiseCanExecuteChanged();
        _clearSelection.RaiseCanExecuteChanged();
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
}
