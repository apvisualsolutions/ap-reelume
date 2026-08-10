// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Movie;

/// <summary>
/// What the details ask the host to open: which version, and from where. A start position of zero is
/// a deliberate restart rather than an absence of progress.
/// </summary>
public sealed record PlayDetailsRequest(MediaFileId? MediaFileId, TimeSpan StartPosition);

/// <summary>
/// The complete film details: what it is, whether it can be played right now, how far through it the
/// person is, and every version that exists. No version is ever hidden, and none of them shows a path.
/// </summary>
public sealed class MovieDetailsViewModel : INotifyPropertyChanged
{
    private static readonly MediaVersionSelectionPolicy SelectionPolicy = new();
    private readonly Func<PlayDetailsRequest, Task>? _onPlay;
    private CatalogItem? _item;
    private WatchState? _watchState;
    private IReadOnlyList<MediaVersionRowViewModel> _versions = [];

    public MovieDetailsViewModel(
        Func<PlayDetailsRequest, Task>? onPlay = null,
        Func<ApSolutions.LocalMedia.Domain.Continuity.WatchStatus?, Task>? onWatchStatusChanged = null,
        Func<PersonalActionRequest, Task>? onPersonalActionChanged = null)
    {
        _onPlay = onPlay;
        WatchStatus = new WatchStatusViewModel(onWatchStatusChanged);
        PersonalActions = new PersonalActionsViewModel(onPersonalActionChanged);
        PlayCommand = new AsyncRelayCommand(() => PlayAsync(fromStart: true), () => CanPlay);
        ResumeCommand = new AsyncRelayCommand(() => PlayAsync(fromStart: false), () => CanResume);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WatchStatusViewModel WatchStatus { get; }

    /// <summary>Favourite, watch later, and rating for this film.</summary>
    public PersonalActionsViewModel PersonalActions { get; }

    public ICommand PlayCommand { get; }

    public ICommand ResumeCommand { get; }

    /// <summary>Which title is shown, so the host can key personal marks to the content.</summary>
    public TitleId TitleId => _item?.Id ?? default;

    public string Title => _item?.Title ?? string.Empty;

    public int? Year => _item?.Year;

    public string YearText => Year is { } year ? year.ToString(CultureInfo.CurrentCulture) : string.Empty;

    public bool HasYear => Year is not null;

    public bool IsAvailable => _item?.IsAvailable ?? false;

    /// <summary>Nothing can be opened while the file is out of reach, so the action is disabled.</summary>
    public bool CanPlay => IsAvailable;

    /// <summary>
    /// True only when there is stored progress worth returning to. Trivial progress near the start is
    /// never offered, and neither is progress on something that cannot be opened.
    /// </summary>
    public bool CanResume => IsAvailable
        && _watchState is { } state
        && ProgressPolicy.ShouldOfferResume(ResumePosition, state.ObservedDuration);

    public TimeSpan ResumePosition => _watchState is { } state
        ? ProgressPolicy.ClampPosition(state.Position, state.ObservedDuration)
        : TimeSpan.Zero;

    public string ResumePositionText => FormatPosition(ResumePosition);

    public IReadOnlyList<MediaVersionRowViewModel> Versions
    {
        get => _versions;
        private set => SetField(ref _versions, value);
    }

    public bool HasVersions => Versions.Count > 0;

    /// <summary>Applies what was already read; the view never queries anything itself.</summary>
    public void Apply(
        CatalogItem item,
        WatchState? watchState,
        MediaVersionGroup? versions,
        PersonalState? personalState = null)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _watchState = watchState;
        Versions = BuildVersions(versions);
        PersonalActions.Apply(
            personalState ?? PersonalState.Empty(ContentKey.ForTitle(item.Id)));
        WatchStatus.Apply(
            watchState?.Status ?? ApSolutions.LocalMedia.Domain.Continuity.WatchStatus.NotStarted,
            watchState?.IsManualOverride ?? false);
        foreach (var name in new[]
        {
            nameof(Title),
            nameof(Year),
            nameof(YearText),
            nameof(HasYear),
            nameof(IsAvailable),
            nameof(CanPlay),
            nameof(CanResume),
            nameof(ResumePosition),
            nameof(ResumePositionText),
            nameof(HasVersions),
        })
        {
            OnPropertyChanged(name);
        }

        (PlayCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ResumeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    internal static string FormatPosition(TimeSpan position) =>
        position.ToString(position >= TimeSpan.FromHours(1) ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.CurrentCulture);

    private static IReadOnlyList<MediaVersionRowViewModel> BuildVersions(MediaVersionGroup? group)
    {
        if (group is null || group.Versions.Count == 0)
        {
            return [];
        }

        var effective = SelectionPolicy
            .Select(group, new MediaVersionPreferences(PreferHdr: true))
            .EffectiveVersion;
        return
        [
            .. group.Versions.Select(version => new MediaVersionRowViewModel(
                version,
                version.MediaFileId == group.PreferredMediaFileId,
                version.MediaFileId == effective?.MediaFileId))
        ];
    }

    private Task PlayAsync(bool fromStart)
    {
        if (_onPlay is null)
        {
            return Task.CompletedTask;
        }

        var version = Versions.FirstOrDefault(row => row.IsEffective);
        return _onPlay(new PlayDetailsRequest(
            version?.MediaFileId,
            fromStart ? TimeSpan.Zero : ResumePosition));
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

/// <summary>
/// One version of the same film. It states resolution, codec and range, which is what tells two
/// versions apart, and never the path they came from.
/// </summary>
public sealed class MediaVersionRowViewModel(MediaVersion version, bool isPreferred, bool isEffective)
{
    private readonly MediaVersion _version = version ?? throw new ArgumentNullException(nameof(version));

    public MediaFileId MediaFileId => _version.MediaFileId;

    public bool IsAvailable => _version.IsAvailable;

    /// <summary>True when the person pinned this version by hand.</summary>
    public bool IsPreferred { get; } = isPreferred;

    /// <summary>True for the version the selection policy would actually open.</summary>
    public bool IsEffective { get; } = isEffective;

    public string QualityLabel => string.Join(
        " · ",
        new[]
        {
            _version is { Width: { } width, Height: { } height }
                ? string.Create(CultureInfo.CurrentCulture, $"{width}×{height}")
                : null,
            string.IsNullOrWhiteSpace(_version.VideoCodec) ? null : _version.VideoCodec,
            _version.IsHdr ? "HDR" : null,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
