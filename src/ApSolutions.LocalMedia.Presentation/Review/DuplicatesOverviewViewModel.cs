// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Review;

/// <summary>One group on the duplicates destination: the title, and how many files answer to it.</summary>
public sealed class DuplicateGroupRowViewModel(DuplicateOverviewEntry entry)
{
    private readonly DuplicateOverviewEntry _entry =
        entry ?? throw new ArgumentNullException(nameof(entry));

    public TitleId TitleId => _entry.TitleId;

    public string Title => _entry.Title;

    public int VersionCount => _entry.VersionCount;
}

/// <summary>
/// The rail's duplicates destination: every title that resolves to more than one file, and the way
/// into the comparison the review has always drawn. The overview owns the list; opening a row goes
/// through the same door the film card's own duplicates action uses.
/// </summary>
public sealed class DuplicatesOverviewViewModel : INotifyPropertyChanged
{
    private readonly GetDuplicateOverview _overview;
    private IReadOnlyList<DuplicateGroupRowViewModel> _groups = [];

    public DuplicatesOverviewViewModel(GetDuplicateOverview overview)
    {
        _overview = overview ?? throw new ArgumentNullException(nameof(overview));
        OpenGroupCommand = new AsyncRelayCommand(
            async parameter =>
            {
                if (parameter is DuplicateGroupRowViewModel row && GroupOpener is { } open)
                {
                    await open(row.TitleId, CancellationToken.None).ConfigureAwait(true);
                }
            },
            parameter => parameter is DuplicateGroupRowViewModel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<DuplicateGroupRowViewModel> Groups
    {
        get => _groups;
        private set
        {
            if (SetField(ref _groups, value))
            {
                OnPropertyChanged(nameof(HasGroups));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public bool HasGroups => Groups.Count > 0;

    /// <summary>
    /// The empty state is the desirable one and says so: every title resolves to a single file, and
    /// nothing was deleted to get there — the approved pair of sentences, not a sad blank.
    /// </summary>
    public bool IsEmpty => Groups.Count == 0;

    /// <summary>Opens one group's comparison. The shell hands this in, the way it hands the card's.</summary>
    public Func<TitleId, CancellationToken, Task>? GroupOpener { get; set; }

    public ICommand OpenGroupCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _overview.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Groups = [.. entries.Select(entry => new DuplicateGroupRowViewModel(entry))];
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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
}
