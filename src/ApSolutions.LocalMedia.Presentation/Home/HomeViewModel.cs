// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Home;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;
using ApSolutions.LocalMedia.Presentation.Navigation;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>
/// The hybrid Home. Continue is the primary action whenever there is something worth continuing, and
/// the library stays one keystroke away either way, so the shortcut never becomes a detour.
/// </summary>
public sealed class HomeViewModel : INotifyPropertyChanged
{
    private readonly GetHome _getHome;
    private readonly INavigationService? _navigation;
    private readonly Func<ContentKey, Task>? _onResume;
    private HomeSnapshot _snapshot = new(null, [], [], new LibrarySummary(0, 0, 0));
    private IReadOnlyList<InProgressItemViewModel> _inProgress = [];
    private IReadOnlyList<RecentlyAddedItemViewModel> _recentlyAdded = [];

    public HomeViewModel(
        GetHome getHome,
        INavigationService? navigation = null,
        Func<ContentKey, Task>? onResume = null,
        RecommendationsViewModel? recommendations = null)
    {
        _getHome = getHome ?? throw new ArgumentNullException(nameof(getHome));
        _navigation = navigation;
        _onResume = onResume;
        Recommendations = recommendations;
        ResumeCommand = new AsyncRelayCommand(ResumeAsync, () => HasResume);
        OpenLibraryCommand = new AsyncRelayCommand(OpenLibraryAsync);
        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(CancellationToken.None));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ResumeCommand { get; }

    public ICommand OpenLibraryCommand { get; }

    public ICommand RefreshCommand { get; }

    /// <summary>The suggestions rail, absent when the host does not compose one.</summary>
    public RecommendationsViewModel? Recommendations { get; }

    public bool HasRecommendationsRail => Recommendations is not null;

    /// <summary>True when Continue is a real action, which is what makes it the first focus.</summary>
    public bool HasResume => _snapshot.Resume is not null;

    public string ResumeTitle => _snapshot.Resume?.Title ?? string.Empty;

    /// <summary>Season and episode for a series, empty for a film; never a path or a file name.</summary>
    public string ResumeSubtitle => _snapshot.Resume is { SeasonNumber: { } season, EpisodeNumber: { } episode }
        ? string.Create(
            CultureInfo.CurrentCulture,
            $"T{season} · E{episode}{(_snapshot.Resume.EpisodeTitle is { Length: > 0 } title ? $" · {title}" : string.Empty)}")
        : string.Empty;

    public bool HasResumeSubtitle => ResumeSubtitle.Length > 0;

    public double ResumeCompletedFraction => _snapshot.Resume?.CompletedFraction ?? 0;

    public string ResumeCompletedText => FormatPercentage(ResumeCompletedFraction);

    public IReadOnlyList<InProgressItemViewModel> InProgress
    {
        get => _inProgress;
        private set => SetField(ref _inProgress, value);
    }

    public IReadOnlyList<RecentlyAddedItemViewModel> RecentlyAdded
    {
        get => _recentlyAdded;
        private set => SetField(ref _recentlyAdded, value);
    }

    public bool HasInProgress => InProgress.Count > 0;

    public bool HasRecentlyAdded => RecentlyAdded.Count > 0;

    public int MovieCount => _snapshot.Library.MovieCount;

    public int ShowCount => _snapshot.Library.ShowCount;

    public int UnavailableCount => _snapshot.Library.UnavailableCount;

    public bool HasUnavailable => UnavailableCount > 0;

    /// <summary>The counts as one string, so the summary is never colour or position alone.</summary>
    public string LibrarySummaryText => string.Create(
        CultureInfo.CurrentCulture,
        $"{MovieCount} · {ShowCount}");

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Apply(await _getHome.ExecuteAsync(new GetHomeQuery(), cancellationToken).ConfigureAwait(false));
        if (Recommendations is { } recommendations)
        {
            await recommendations.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Applies an already-read snapshot; the view never decides what Home contains.</summary>
    public void Apply(HomeSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        InProgress = [.. snapshot.InProgress.Select(item => new InProgressItemViewModel(item))];
        RecentlyAdded = [.. snapshot.RecentlyAdded.Select(item => new RecentlyAddedItemViewModel(item))];
        foreach (var name in new[]
        {
            nameof(HasResume),
            nameof(ResumeTitle),
            nameof(ResumeSubtitle),
            nameof(HasResumeSubtitle),
            nameof(ResumeCompletedFraction),
            nameof(ResumeCompletedText),
            nameof(HasInProgress),
            nameof(HasRecentlyAdded),
            nameof(MovieCount),
            nameof(ShowCount),
            nameof(UnavailableCount),
            nameof(HasUnavailable),
            nameof(LibrarySummaryText),
        })
        {
            OnPropertyChanged(name);
        }

        (ResumeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    internal static string FormatPercentage(double fraction) =>
        Math.Round(fraction * 100).ToString("F0", CultureInfo.CurrentCulture);

    private Task ResumeAsync() => _snapshot.Resume is { } resume && _onResume is not null
        ? _onResume(resume.Content)
        : Task.CompletedTask;

    private Task OpenLibraryAsync()
    {
        _navigation?.Navigate(AppRoute.Library);
        return Task.CompletedTask;
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

/// <summary>One card of the in-progress rail. An unreachable file is shown, never quietly dropped.</summary>
public sealed class InProgressItemViewModel(InProgressItem item)
{
    private readonly InProgressItem _item = item ?? throw new ArgumentNullException(nameof(item));

    public ContentKey Content => _item.Content;

    public TitleId TitleId => _item.TitleId;

    public string Title => _item.Title;

    public string Subtitle => _item is { SeasonNumber: { } season, EpisodeNumber: { } episode }
        ? string.Create(CultureInfo.CurrentCulture, $"T{season} · E{episode}")
        : string.Empty;

    public bool HasSubtitle => Subtitle.Length > 0;

    public bool IsAvailable => _item.IsAvailable;

    public bool IsShow => _item.Kind == CatalogTitleKind.Show;

    public double CompletedFraction => _item.CompletedFraction;

    public string CompletedText => HomeViewModel.FormatPercentage(_item.CompletedFraction);
}

/// <summary>One card of the recently added rail.</summary>
public sealed class RecentlyAddedItemViewModel(RecentlyAddedItem item)
{
    private readonly RecentlyAddedItem _item = item ?? throw new ArgumentNullException(nameof(item));

    public TitleId Id => _item.Id;

    public string Title => _item.Title;

    public string YearText => _item.Year is { } year
        ? year.ToString(CultureInfo.CurrentCulture)
        : string.Empty;

    public bool HasYear => _item.Year is not null;

    public bool IsAvailable => _item.IsAvailable;

    public bool IsShow => _item.Kind == CatalogTitleKind.Show;
}
