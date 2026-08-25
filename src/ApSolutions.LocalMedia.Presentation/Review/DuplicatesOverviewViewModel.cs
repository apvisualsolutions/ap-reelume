// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Review;

/// <summary>
/// One file of a duplicate group, as the prototype's table writes it: what it is called, what it is
/// made of, how big it is, how long it runs, where it lives, and whether it is reachable.
/// </summary>
public sealed class DuplicateFileRowViewModel
{
    private readonly DuplicateFileRow _row;

    public DuplicateFileRowViewModel(DuplicateFileRow row, ICommand setPreferred)
    {
        _row = row ?? throw new ArgumentNullException(nameof(row));
        SetPreferredCommand = setPreferred ?? throw new ArgumentNullException(nameof(setPreferred));
    }

    public DuplicateFileRow Row => _row;

    public MediaFileId MediaFileId => _row.MediaFileId;

    /// <summary>
    /// The tail of the path, which is what tells two copies apart: the folder is a column of its own
    /// and repeating it in every file name would push the facts off the side.
    /// </summary>
    public string ShortPath => Shortened(_row.Path);

    /// <summary>
    /// The tail of a path. Written as a length rather than as a pattern that also tests for null:
    /// GetFileName answers with an empty string for a folder, never with nothing, and a test for a
    /// state the framework cannot produce is a branch nothing can take.
    /// </summary>
    private static string Shortened(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.Length > 0 ? "..." + name : path;
    }

    public string Resolution => _row is { Width: { } width, Height: { } height }
        ? string.Create(CultureInfo.CurrentCulture, $"{width} × {height}")
        : string.Empty;

    public string VideoCodec => _row.VideoCodec;

    public string AudioCodec => _row.AudioCodec;

    /// <summary>Gigabytes with one decimal, or megabytes under one.</summary>
    public string Size
    {
        get
        {
            const double Mega = 1024d * 1024d;
            if (_row.SizeBytes <= 0)
            {
                return string.Empty;
            }

            var megabytes = _row.SizeBytes / Mega;
            return megabytes >= 1024
                ? string.Create(CultureInfo.CurrentCulture, $"{megabytes / 1024:0.0} GB")
                : string.Create(CultureInfo.CurrentCulture, $"{megabytes:0} MB");
        }
    }

    public string Duration => _row.Duration is { Ticks: > 0 } duration
        ? Player.PlaybackClock.Format(duration)
        : string.Empty;

    /// <summary>The folder, which is how a person tells a disc from a backup drive.</summary>
    public string Location => System.IO.Path.GetDirectoryName(_row.Path) ?? string.Empty;

    public bool IsAvailable => _row.IsAvailable;

    public bool IsPreferred => _row.IsPreferred;

    public ICommand SetPreferredCommand { get; }
}

/// <summary>One group on the duplicates destination: the title, and the files that answer to it.</summary>
public sealed class DuplicateGroupRowViewModel(DuplicateOverviewEntry entry)
{
    private readonly DuplicateOverviewEntry _entry =
        entry ?? throw new ArgumentNullException(nameof(entry));

    public TitleId TitleId => _entry.TitleId;

    public string Title => _entry.Title;

    public int VersionCount => _entry.VersionCount;

    public MediaVersionId GroupId => _entry.GroupId;

    /// <summary>The files themselves, which is what the destination is for.</summary>
    public IReadOnlyList<DuplicateFileRowViewModel> Files { get; internal set; } = [];

    public bool HasFiles => Files.Count > 0;
}

/// <summary>
/// The rail's duplicates destination: every title that resolves to more than one file, and the way
/// into the comparison the review has always drawn. The overview owns the list; opening a row goes
/// through the same door the film card's own duplicates action uses.
/// </summary>
public sealed class DuplicatesOverviewViewModel : INotifyPropertyChanged
{
    private readonly GetDuplicateOverview _overview;
    private readonly SetPreferredVersion _setPreferredVersion;
    private IReadOnlyList<DuplicateGroupRowViewModel> _groups = [];

    public DuplicatesOverviewViewModel(GetDuplicateOverview overview, SetPreferredVersion setPreferredVersion)
    {
        _overview = overview ?? throw new ArgumentNullException(nameof(overview));
        _setPreferredVersion = setPreferredVersion ?? throw new ArgumentNullException(nameof(setPreferredVersion));
        OpenGroupCommand = new AsyncRelayCommand(
            async parameter =>
            {
                if (parameter is DuplicateGroupRowViewModel row && GroupOpener is { } open)
                {
                    await open(row.TitleId, CancellationToken.None).ConfigureAwait(true);
                }
            },
            parameter => parameter is DuplicateGroupRowViewModel);
        SetPreferredCommand = new AsyncRelayCommand(
            SetPreferredAsync,
            parameter => parameter is DuplicateFileRowViewModel);
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

    /// <summary>
    /// Chooses which copy of a title plays by default. The choice is stored and the list is read
    /// again, so what the radios show is what the catalogue holds rather than what was clicked.
    /// </summary>
    public ICommand SetPreferredCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var entries = await _overview.ExecuteAsync(cancellationToken).ConfigureAwait(true);
        Groups =
        [
            .. entries.Select(entry =>
            {
                var row = new DuplicateGroupRowViewModel(entry);
                row.Files =
                [
                    .. (entry.Files ?? []).Select(file =>
                        new DuplicateFileRowViewModel(file, SetPreferredCommand))
                ];
                return row;
            })
        ];
    }

    /// <summary>
    /// Stores the choice for the group the file belongs to, then reloads. The group is found from
    /// the file rather than carried beside it: a radio knows which row it is on and nothing else,
    /// and a command parameter that had to carry both would be a pair nobody could see was wrong.
    /// </summary>
    private async Task SetPreferredAsync(object? parameter)
    {
        // The parameter is not tested again: the command refuses anything that is not a row before
        // this runs, so a second check would be a branch nothing can take.
        var file = (DuplicateFileRowViewModel)parameter!;
        var group = Groups.FirstOrDefault(candidate =>
            candidate.Files.Any(row => row.MediaFileId == file.MediaFileId));
        if (group is null)
        {
            return;
        }

        _ = await _setPreferredVersion.ExecuteAsync(
            new SetPreferredVersionCommand(group.GroupId, file.MediaFileId),
            CancellationToken.None).ConfigureAwait(true);
        await LoadAsync(CancellationToken.None).ConfigureAwait(true);
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
