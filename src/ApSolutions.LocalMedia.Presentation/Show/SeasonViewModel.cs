// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Presentation.Show;

/// <summary>
/// The few strings the series card has to assemble rather than pick.
/// </summary>
/// <remarks>
/// Three classes of this card need it — the season's own name, the row's running time and status,
/// and the banner's counts — and a private copy in each of them would be the same six lines written
/// three times. The fallback is not decoration: a headless test mounts these without the string
/// dictionaries, and a null there would print a blank rather than failing loudly.
/// </remarks>
internal static class ShowText
{
    public static string Resource(string key, string fallback) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            && value is string text
                ? text
                : fallback;

    /// <summary>The same, with one placeholder filled in.</summary>
    public static string Format(string key, string fallback, string value) =>
        Resource(key, fallback).Replace("{0}", value, StringComparison.Ordinal);
}

/// <summary>
/// One season and its episodes in viewing order. Season zero is the specials season and says so in
/// words, because it is a deliberate choice rather than part of the run.
/// </summary>
public sealed class SeasonViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public SeasonViewModel(int seasonNumber, IReadOnlyList<EpisodeRowViewModel> episodes)
    {
        ArgumentNullException.ThrowIfNull(episodes);
        ArgumentOutOfRangeException.ThrowIfNegative(seasonNumber);
        SeasonNumber = seasonNumber;
        Episodes = episodes;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int SeasonNumber { get; }

    /// <summary>
    /// Whether this is the season on screen, which is what fills its pill.
    /// </summary>
    /// <remarks>
    /// The chooser was a dropdown, so the two seasons you were not on lived behind a click. The
    /// prototype puts every season on the surface, and a pill that cannot say whether it is the
    /// chosen one is a row of identical buttons — so the state lives here rather than in the view,
    /// where a second season could quietly light up beside the first.
    /// </remarks>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsSpecials => SeasonNumber == 0;

    public bool IsRegular => !IsSpecials;

    public string SeasonNumberText => SeasonNumber.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// «Temporada 2», or «Especiales» — what the pill says, and what a reader user hears.
    /// </summary>
    /// <remarks>
    /// It is one string rather than three <c>TextBlock</c>s with visibility bindings because a pill
    /// is announced as a whole: a control assembled out of two shown labels and one hidden one reads
    /// out as whatever happened to be visible, and the walk has nothing to aim at.
    /// </remarks>
    public string SeasonLabel => IsSpecials
        ? ShowText.Resource("SeasonSpecialsHeading", "Specials")
        : string.Create(
            CultureInfo.CurrentCulture,
            $"{ShowText.Resource("SeasonHeading", "Season")} {SeasonNumber}");

    public IReadOnlyList<EpisodeRowViewModel> Episodes { get; }

    public int EpisodeCount => Episodes.Count;

    public string EpisodeCountText => EpisodeCount.ToString(CultureInfo.CurrentCulture);

    /// <summary>How many of this season's episodes are finished.</summary>
    public int WatchedCount => Episodes.Count(episode => episode.IsWatched);
}

/// <summary>
/// One episode row: where it sits, whether it can be opened right now, and how far through it the
/// person is. An episode with no file stays listed so the season never looks shorter than it is.
/// </summary>
public sealed class EpisodeRowViewModel
{
    private readonly EpisodeSequenceEntry _entry;
    private readonly WatchState? _watchState;

    public EpisodeRowViewModel(EpisodeSequenceEntry entry, WatchState? watchState, string? showTitle = null)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
        _watchState = watchState;
        ShowTitle = showTitle ?? string.Empty;
    }

    /// <summary>
    /// The series this episode belongs to, which is what its still is coloured from.
    /// </summary>
    /// <remarks>
    /// The show's title and not the episode's, because the prototype draws a season as one family of
    /// tones — <c>art(show.h + episode * 7, 'w')</c> — and hashing each episode's own name turned a
    /// list of sixteen into sixteen unrelated colours. Empty when a row is built without one, which
    /// a headless mount does; an empty title has a hue like any other.
    /// </remarks>
    public string ShowTitle { get; }

    /// <summary>How far around the wheel this episode's still is turned from the show's own hue.</summary>
    public int ArtHueShift => EpisodeNumber * 7;

    public EpisodeId Id => _entry.Id;

    public int SeasonNumber => _entry.SeasonNumber;

    public int EpisodeNumber => _entry.EpisodeNumber;

    public string EpisodeNumberText => EpisodeNumber.ToString(CultureInfo.CurrentCulture);

    /// <summary>
    /// Which episode this row is, in the form every catalogue writes it. Ten "Play episode" buttons
    /// announce the same sentence without it, so a reader user cannot tell which one they are on.
    /// </summary>
    public string SeasonEpisodeLabel => string.Create(
        CultureInfo.CurrentCulture,
        $"S{SeasonNumber:D2}E{EpisodeNumber:D2}");

    public MediaFileId? MediaFileId => _entry.MediaFileId;

    public bool IsAvailable => _entry.IsAvailable;

    /// <summary>True only when a reachable file exists behind this episode.</summary>
    public bool IsPlayable => _entry.IsPlayable;

    public WatchStatus WatchStatus => _watchState?.Status ?? Domain.Continuity.WatchStatus.NotStarted;

    public bool IsWatched => WatchStatus == Domain.Continuity.WatchStatus.Watched;

    public bool IsInProgress => WatchStatus == Domain.Continuity.WatchStatus.InProgress;

    public bool IsNotStarted => WatchStatus == Domain.Continuity.WatchStatus.NotStarted;

    /// <summary>True while a decision made by hand is in force, announced in words rather than colour.</summary>
    public bool IsManualOverride => _watchState?.IsManualOverride ?? false;

    /// <summary>«E01» — the number as the prototype's row badge writes it, two digits and padded.</summary>
    public string NumberBadge => string.Create(CultureInfo.CurrentCulture, $"E{EpisodeNumber:D2}");

    /// <summary>
    /// What this episode is called. The catalogue's own title, and the season-episode label when
    /// nobody has identified it — a row with no name at all is a list of numbers.
    /// </summary>
    public string EpisodeTitle => string.IsNullOrWhiteSpace(_entry.Title)
        ? SeasonEpisodeLabel
        : _entry.Title;

    /// <summary>
    /// «48 min · Visto», or «48 min · Reanudar en 17:00», which is the prototype's second line.
    /// </summary>
    /// <remarks>
    /// The status was three <c>TextBlock</c>s with visibility bindings beside the running time, so a
    /// row said its state in whichever of them happened to be shown. One string, because there is
    /// one answer: how long it runs, and how far through it you are.
    /// </remarks>
    public string MetaText => string.Join(
        " · ",
        new[] { RuntimeText, StatusText }.Where(part => part.Length > 0));

    /// <summary>How far through this episode the person is, between nothing and one.</summary>
    public double CompletedFraction
    {
        get
        {
            if (_watchState is not { } state)
            {
                return 0;
            }

            var duration = _entry.Runtime ?? state.ObservedDuration;
            return duration is { Ticks: > 0 } span
                ? Math.Clamp(state.Position.Ticks / (double)span.Ticks, 0, 1)
                : 0;
        }
    }

    /// <summary>The bar across the still, which only exists once something has been watched.</summary>
    public bool HasProgress => CompletedFraction > 0;

    private string RuntimeText => _entry.Runtime is { } runtime && runtime > TimeSpan.Zero
        ? ShowText.Format(
            "CatalogRuntimeMinutes",
            "{0} min",
            ((int)Math.Round(runtime.TotalMinutes)).ToString(CultureInfo.CurrentCulture))
        : string.Empty;

    private string StatusText => WatchStatus switch
    {
        Domain.Continuity.WatchStatus.Watched => ShowText.Resource("WatchStatusWatched", "Watched"),
        Domain.Continuity.WatchStatus.InProgress when _watchState is { } state && state.Position > TimeSpan.Zero =>
            ShowText.Format("EpisodeResumeAt", "Resume at {0}", Player.PlaybackClock.Format(state.Position)),
        Domain.Continuity.WatchStatus.InProgress => ShowText.Resource("WatchStatusInProgress", "In progress"),
        _ => ShowText.Resource("WatchStatusNotStarted", "Not started"),
    };
}
