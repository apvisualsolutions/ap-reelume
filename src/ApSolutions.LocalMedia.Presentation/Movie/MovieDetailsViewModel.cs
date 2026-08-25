// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Movie;

/// <summary>
/// What the details ask the host to open: which version, and from where. A start position of zero is
/// a deliberate restart rather than an absence of progress, which is why the absence has a value of
/// its own: <see langword="null"/> asks the host to decide with the resume policy.
/// </summary>
/// <remarks>
/// The distinction is not decoration. Until 2026-08-17 the host read the file and ignored the
/// position, so every caller that had already worked out where to open was overruled by a policy
/// reading storage under the <b>new</b> file's content key — and a confirmed version switch, whose
/// whole point is the second the person agreed to carry across, opened the other version at zero and
/// then wrote that zero over the position it had just transferred. Measured on the walk: the
/// playhead read 0, 0, 0, 0, 1, 1, 1, 2 while the switch had stored 00:02:01.
/// </remarks>
/// <summary>
/// What to play, from where, and what it is called.
/// </summary>
/// <remarks>
/// The last two arrived on 2026-08-25. The player's header had nothing to write in the middle of it
/// — the session holds a path, and painting somebody's file path as a heading is the opposite of
/// what this application is for — while every surface that starts a session already knows the title
/// and the line under it. So they travel with the request rather than being looked up again: the
/// card that pressed Play is the one that knows.
/// </remarks>
public sealed record PlayDetailsRequest(
    MediaFileId? MediaFileId,
    TimeSpan? StartPosition,
    string? Title = null,
    string? Subtitle = null);

/// <summary>
/// The complete film details: what it is, whether it can be played right now, how far through it the
/// person is, and every version that exists. No version is ever hidden, and none of them shows a path.
/// </summary>
public sealed class MovieDetailsViewModel : INotifyPropertyChanged
{
    private static readonly MediaVersionSelectionPolicy SelectionPolicy = new();
    private readonly Func<PlayDetailsRequest, Task>? _onPlay;
    private readonly Func<string, Task>? _onPlayTrailer;
    private readonly Func<string, Task>? _onOpenTrailerLink;
    private string? _trailerPath;
    private string? _trailerLink;
    private CatalogItem? _item;
    private string? _overview;
    private WatchState? _watchState;
    private bool _renameWouldChangeTheName;
    private MediaFile? _file;
    private IReadOnlyList<MediaVersionRowViewModel> _versions = [];

    public MovieDetailsViewModel(
        Func<PlayDetailsRequest, Task>? onPlay = null,
        Func<ApSolutions.LocalMedia.Domain.Continuity.WatchStatus?, Task>? onWatchStatusChanged = null,
        Func<PersonalActionRequest, Task>? onPersonalActionChanged = null,
        Func<string, Task>? onPlayTrailer = null,
        Func<string, Task>? onOpenTrailerLink = null)
    {
        _onPlay = onPlay;
        _onPlayTrailer = onPlayTrailer;
        _onOpenTrailerLink = onOpenTrailerLink;
        PlayTrailerCommand = new AsyncRelayCommand(PlayTrailerAsync, () => HasTrailer);
        OpenTrailerLinkCommand = new AsyncRelayCommand(OpenTrailerLinkAsync, () => HasTrailerLink);
        WatchStatus = new WatchStatusViewModel(onWatchStatusChanged);
        PersonalActions = new PersonalActionsViewModel(onPersonalActionChanged);
        PlayCommand = new AsyncRelayCommand(() => PlayAsync(fromStart: true), () => CanPlay);
        ResumeCommand = new AsyncRelayCommand(() => PlayAsync(fromStart: false), () => CanResume);
        PlayOrResumeCommand = new AsyncRelayCommand(
            () => PlayAsync(fromStart: !CanResume),
            () => CanPlay);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WatchStatusViewModel WatchStatus { get; }

    /// <summary>Favourite, watch later, and rating for this film.</summary>
    public PersonalActionsViewModel PersonalActions { get; }

    public ICommand PlayCommand { get; }

    public ICommand ResumeCommand { get; }

    /// <summary>
    /// What the leading action runs: the stored minute when there is one, and zero when there is not.
    /// </summary>
    /// <remarks>
    /// The two remain separately reachable — the glyph beside this button is <c>PlayCommand</c>, and
    /// the shortcut a keyboard reaches is <c>ResumeCommand</c> — because they answer different
    /// questions. This one answers the button's own question, which is «open it».
    /// </remarks>
    public ICommand PlayOrResumeCommand { get; }

    /// <summary>
    /// Opens the trailer that already exists next to the film. It is a loose session on purpose: a
    /// trailer is not a catalogue row and must not become one, and nothing is downloaded to play it.
    /// </summary>
    public ICommand PlayTrailerCommand { get; }

    /// <summary>
    /// Opens the provider's trailer in the browser (LIB-015). This is a separate offer from the one
    /// above and it leaves the application on purpose: playing YouTube inside would need a route
    /// their terms do not allow. The address was composed by <see cref="TrailerLinkPolicy"/> when
    /// the key arrived, so what travels from here is already the only shape this application builds.
    /// </summary>
    public ICommand OpenTrailerLinkCommand { get; }

    /// <summary>True only when a file on the disk was found to be this film's trailer.</summary>
    public bool HasTrailer => !string.IsNullOrWhiteSpace(_trailerPath);

    /// <summary>
    /// True only when the stored key was well formed. A key that is not — the provider changed the
    /// shape, the row was edited by hand — offers nothing, rather than offering a button that opens
    /// something unknown.
    /// </summary>
    public bool HasTrailerLink => _trailerLink is not null;

    /// <summary>Which title is shown, so the host can key personal marks to the content.</summary>
    public TitleId TitleId => _item?.Id ?? default;

    public string Title => _item?.Title ?? string.Empty;

    /// <summary>The banner poster's two letters, from the same well the grid's cards drink.</summary>
    public string Initials => Library.PosterInitials.From(Title);

    /// <summary>
    /// What this film is about, as the stored metadata has it. It is handed in rather than read here,
    /// the same way the watch state and the versions are: this view model queries nothing.
    /// </summary>
    public string? Overview => _overview;

    /// <summary>
    /// True only for a synopsis with something in it. Blank is absent: a heading over an empty block
    /// reads as a defect to somebody using a screen reader, and to everybody else as an application
    /// pretending to show what it does not have.
    /// </summary>
    public bool HasOverview => !string.IsNullOrWhiteSpace(_overview);

    public int? Year => _item?.Year;

    public string YearText => Year is { } year ? year.ToString(CultureInfo.CurrentCulture) : string.Empty;

    public bool HasYear => Year is not null;

    /// <summary>
    /// «2019 · Drama · 118 min», which is the line the prototype writes under the title.
    /// </summary>
    /// <remarks>
    /// The card had a year and nothing else, because a year was all the read model carried. It
    /// carries the running time and the genres since 2026-08-24, so the line the prototype has
    /// always written can finally be written — the same one the library's own card gained, in the
    /// order the banner uses: what year, what kind of story, how long.
    /// </remarks>
    public string MetaText => string.Join(
        " · ",
        new[]
        {
            YearText,
            _item?.Genres is { Count: > 0 } genres ? string.Join(" · ", genres.Take(2)) : string.Empty,
            _item?.Runtime is { } runtime && runtime > TimeSpan.Zero
                ? RuntimeText(runtime)
                : string.Empty,
        }.Where(piece => piece.Length > 0));

    public bool HasMeta => MetaText.Length > 0;

    /// <summary>The chip that says whether the file is there, as a resource key.</summary>
    public string AvailabilityKey => IsAvailable ? "MediaAvailable" : "MediaUnavailable";

    /// <summary>And the one that says how far through it is.</summary>
    public string WatchStatusKey => (_watchState?.Status ?? Domain.Continuity.WatchStatus.NotStarted) switch
    {
        Domain.Continuity.WatchStatus.InProgress => "WatchStatusInProgress",
        Domain.Continuity.WatchStatus.Watched => "WatchStatusWatched",
        _ => "WatchStatusNotStarted",
    };

    private static string RuntimeText(TimeSpan runtime) =>
        Resource("CatalogRuntimeMinutes", "{0} min").Replace(
            "{0}",
            ((int)Math.Round(runtime.TotalMinutes)).ToString(CultureInfo.CurrentCulture),
            StringComparison.Ordinal);

    private static string Resource(string key, string fallback) =>
        Avalonia.Application.Current is { } application
            && application.TryGetResource(key, application.ActualThemeVariant, out var value)
            && value is string text
                ? text
                : fallback;

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

    /// <summary>
    /// The words on the one button that opens this film, which follow what there is to open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype writes exactly this — <c>(pos > 0 ? t.resume : t.play) + (pos > 0 ? ' · ' +
    /// fmt(pos) : '')</c> — and this card had the first half only: the button was hidden outright
    /// when there was no progress, so a film nobody had started showed the «from the start» glyph
    /// and nothing else. «En ciertas ocasiones en la vista detalle desaparece el botón de
    /// reproducir/continuar y solamente queda el icono de ver desde el inicio», 2026-08-25, and the
    /// occasion is every film nobody has started.
    /// </para>
    /// <para>
    /// A property and not two buttons swapped by visibility, because the two are one control: it is
    /// the leading action of this view, <c>LeadingActionTests</c> allows exactly one of those per
    /// view, and a pair that took turns would be two controls the walk has to press to prove one.
    /// </para>
    /// </remarks>
    public string PlayActionText => CanResume
        ? Resource("MovieResumeAction", "Resume") + " · " + ResumePositionText
        : Resource("MovieStartAction", "Play");

    public IReadOnlyList<MediaVersionRowViewModel> Versions
    {
        get => _versions;
        private set => SetField(ref _versions, value);
    }

    public bool HasVersions => Versions.Count > 0;

    /// <summary>
    /// Whether there is more than one copy of this film, which is the only case «Revisar versiones»
    /// has anything to open.
    /// </summary>
    /// <remarks>
    /// A film with a single copy is never grouped — <c>GroupMediaVersions</c> refuses fewer than two
    /// — so the surface behind that button answers with nothing and the route never changes. The
    /// button was there anyway, offering a comparison of one thing against itself.
    /// </remarks>
    public bool HasOtherVersions => Versions.Count > 1;

    /// <summary>
    /// Whether renaming this film's file would change its name at all.
    /// </summary>
    /// <remarks>
    /// <c>RenamePolicy</c> already answers it: a request whose destination equals its source becomes
    /// a <c>NoChange</c> conflict rather than an operation, so an empty plan means the file is
    /// already called what this application would call it. Asked when the card is opened, because a
    /// button that opens a preview of nothing is a button that promises work there is none of.
    /// </remarks>
    public bool CanPreviewRename => _renameWouldChangeTheName;

    /// <summary>Applies what was already read; the view never queries anything itself.</summary>
    public void Apply(
        CatalogItem item,
        WatchState? watchState,
        MediaVersionGroup? versions,
        PersonalState? personalState = null,
        string? overview = null,
        string? trailerPath = null,
        string? trailerKey = null,
        MediaFile? file = null,
        bool renameWouldChangeTheName = false)
    {
        _renameWouldChangeTheName = renameWouldChangeTheName;
        _item = item ?? throw new ArgumentNullException(nameof(item));
        _file = file;
        _overview = overview;
        _trailerPath = trailerPath;
        _trailerLink = TrailerLinkPolicy.TryBuildWatchLink(trailerKey);
        _watchState = watchState;
        Versions = versions is null ? BuildLoneVersion() : BuildVersions(versions);
        PersonalActions.Apply(
            personalState ?? PersonalState.Empty(ContentKey.ForTitle(item.Id)));
        WatchStatus.Apply(
            watchState?.Status ?? ApSolutions.LocalMedia.Domain.Continuity.WatchStatus.NotStarted,
            watchState?.IsManualOverride ?? false);
        foreach (var name in new[]
        {
            nameof(Title),
            nameof(Initials),
            nameof(Overview),
            nameof(HasOverview),
            nameof(Year),
            nameof(YearText),
            nameof(HasYear),
            nameof(MetaText),
            nameof(HasMeta),
            nameof(AvailabilityKey),
            nameof(WatchStatusKey),
            nameof(IsAvailable),
            nameof(CanPlay),
            nameof(CanResume),
            nameof(ResumePosition),
            nameof(ResumePositionText),
            nameof(PlayActionText),
            nameof(HasVersions),
            nameof(HasOtherVersions),
            nameof(CanPreviewRename),
            nameof(HasTrailer),
            nameof(HasTrailerLink),
        })
        {
            OnPropertyChanged(name);
        }

        (PlayCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ResumeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PlayOrResumeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PlayTrailerCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (OpenTrailerLinkCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    internal static string FormatPosition(TimeSpan position) =>
        position.ToString(position >= TimeSpan.FromHours(1) ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.CurrentCulture);

    /// <summary>
    /// The single row a film with no duplicates still has.
    /// </summary>
    /// <remarks>
    /// A film is only grouped when there are two of it — <c>GroupMediaVersions</c> refuses fewer
    /// than two — so the ordinary film had no group and this section was simply absent, leaving the
    /// card with an empty half. The prototype lists what there is either way, because the question
    /// "which copy is this and where does it live" is asked of every film and not only of duplicated
    /// ones.
    /// </remarks>
    private IReadOnlyList<MediaVersionRowViewModel> BuildLoneVersion() =>
        _file is { } file
            ? [new MediaVersionRowViewModel(
                new MediaVersion(
                    file.Id,
                    file.Path,
                    file.IsAvailable,
                    file.TechnicalMetadata.Duration,
                    file.TechnicalMetadata.Width,
                    file.TechnicalMetadata.Height,
                    IsHdr: false,
                    file.TechnicalMetadata.VideoCodecs.Count > 0 ? file.TechnicalMetadata.VideoCodecs[0] : string.Empty,
                    file.SizeBytes),
                isPreferred: false,
                isEffective: true)]
            : [];

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

    private Task PlayTrailerAsync() =>
        _onPlayTrailer is null || _trailerPath is not { Length: > 0 } path
            ? Task.CompletedTask
            : _onPlayTrailer(path);

    private Task OpenTrailerLinkAsync() =>
        _onOpenTrailerLink is null || _trailerLink is not { Length: > 0 } link
            ? Task.CompletedTask
            : _onOpenTrailerLink(link);

    private Task PlayAsync(bool fromStart)
    {
        if (_onPlay is null)
        {
            return Task.CompletedTask;
        }

        var version = Versions.FirstOrDefault(row => row.IsEffective);
        return _onPlay(new PlayDetailsRequest(
            version?.MediaFileId,
            fromStart ? TimeSpan.Zero : ResumePosition,
            Title,
            MetaText));
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

    /// <summary>
    /// The second line of a version row: how long it runs and what it weighs.
    /// </summary>
    /// <remarks>
    /// The prototype writes «H.264 · AAC 2.0 · 3,4 GB · 1:51:00» under the quality, and the row here
    /// said the quality and stopped. What this can answer is the length and the size — the audio
    /// track is not in <c>MediaVersion</c>, and inventing a codec name for a row is worse than
    /// leaving the question to the version picker, which reads the file.
    /// </remarks>
    public string TechnicalText => string.Join(
        " · ",
        new[]
        {
            _version.Duration is { } duration && duration > TimeSpan.Zero
                ? Player.PlaybackClock.Format(duration)
                : null,
            _version.SizeBytes > 0 ? SizeText(_version.SizeBytes) : null,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public bool HasTechnical => TechnicalText.Length > 0;

    /// <summary>Where the file is, which is the line the prototype ends a version row with.</summary>
    public string PathText => _version.Path;

    /// <summary>Gigabytes with one decimal, or megabytes under one.</summary>
    private static string SizeText(long bytes)
    {
        const double Mega = 1024d * 1024d;
        var megabytes = bytes / Mega;
        return megabytes >= 1024
            ? string.Create(CultureInfo.CurrentCulture, $"{megabytes / 1024:0.0} GB")
            : string.Create(CultureInfo.CurrentCulture, $"{megabytes:0} MB");
    }
}
