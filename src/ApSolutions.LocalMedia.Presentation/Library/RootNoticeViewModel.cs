// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Presentation.Library;

/// <summary>
/// One line of the notices strip: a root that stopped being readable, and which of the two ways.
/// </summary>
public sealed class RootNoticeRowViewModel(LibraryRootId id, string path, RootAvailability availability)
{
    public LibraryRootId Id { get; } = id;

    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    /// <summary>
    /// The folder is there and Windows refuses it, which is a different thing to go and do about it
    /// than a drive that is gone — so it is a different tone as well as a different sentence, and the
    /// prototype agrees: it paints this one in the error tone and the disconnection in the warning.
    /// </summary>
    public bool IsAccessDenied { get; } = availability == RootAvailability.AccessDenied;

    public string TitleKey => IsAccessDenied ? "LibraryNoticeAccessDeniedTitle" : "LibraryNoticeRootGoneTitle";

    public string BodyKey => IsAccessDenied ? "LibraryNoticeAccessDeniedBody" : "LibraryNoticeRootGoneBody";
}

/// <summary>
/// What the Library says about roots it cannot read, which ADR-0010 puts here and nowhere else: the
/// notice goes where the affected titles are and where somebody can act, not chasing them through
/// the player and the settings.
/// </summary>
/// <remarks>
/// It describes a STATE — the drive is out for as long as it is out — so it takes space and pushes,
/// which is the whole of the ADR's first point. The titles stay marked one by one as they already
/// were; this is the sentence that explains why.
/// <para>
/// The scan is what learns it, and it already wrote it to the root's own row before this existed.
/// What was missing was saying it out loud: the event died inside the method.
/// </para>
/// </remarks>
public sealed class RootNoticeViewModel : INotifyPropertyChanged
{
    private readonly Dictionary<LibraryRootId, RootNoticeRowViewModel> _byRoot = [];

    public RootNoticeViewModel()
    {
    }

    public RootNoticeViewModel(InProcessApplicationEventPublisher eventPublisher)
    {
        ArgumentNullException.ThrowIfNull(eventPublisher);
        eventPublisher.Published += applicationEvent =>
        {
            if (applicationEvent is RootAvailabilityChanged changed)
            {
                Apply(changed);
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RootNoticeRowViewModel> Notices { get; } = [];

    public bool HasNotices => Notices.Count > 0;

    public void Apply(RootAvailabilityChanged changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        if (changed.Availability == RootAvailability.Available)
        {
            if (_byRoot.Remove(changed.RootId, out var resolved))
            {
                _ = Notices.Remove(resolved);
                OnPropertyChanged(nameof(HasNotices));
            }

            return;
        }

        var row = new RootNoticeRowViewModel(changed.RootId, changed.Path, changed.Availability);
        if (_byRoot.TryGetValue(changed.RootId, out var existing))
        {
            // The same root can go from gone to refused without passing through available: a share
            // that comes back up and then rejects the credentials. Replacing keeps one line per root.
            Notices[Notices.IndexOf(existing)] = row;
            _byRoot[changed.RootId] = row;
            return;
        }

        _byRoot[changed.RootId] = row;
        Notices.Add(row);
        OnPropertyChanged(nameof(HasNotices));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
