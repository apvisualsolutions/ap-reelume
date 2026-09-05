// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Onboarding;

/// <summary>One cataloged folder: enough to recognise it and to ask for it to leave.</summary>
public sealed class LibraryRootRowViewModel(LibraryRoot root)
{
    private readonly LibraryRoot _root = root ?? throw new ArgumentNullException(nameof(root));

    public LibraryRootId Id => _root.Id;

    public string Path => _root.Path;

    /// <summary>
    /// The kind and the availability, as resource keys the surface translates — the same shape
    /// <c>CatalogItemViewModel.AvailabilityKey</c> already has, and for the same reason: the words
    /// follow the chosen language instead of being decided when the row was read.
    /// </summary>
    public string KindKey => _root.Kind switch
    {
        RootKind.Usb => "RootKindUsb",
        RootKind.Unc => "RootKindUnc",
        _ => "RootKindLocal",
    };

    public bool IsAvailable => _root.Availability == RootAvailability.Available;

    /// <summary>
    /// The drive is not there. Plugging it back in is what fixes it, so it gets its own word.
    /// </summary>
    public bool IsDisconnected => _root.Availability == RootAvailability.Unavailable;

    /// <summary>
    /// The folder is there and Windows refuses it — a share whose credentials expired, a disk that
    /// belongs to another user. Saying "Unavailable" here sends somebody to look for a cable that is
    /// already plugged in, which is why the third state stopped sharing the second one's sentence.
    /// </summary>
    public bool IsAccessDenied => _root.Availability == RootAvailability.AccessDenied;

    public string AvailabilityKey => _root.Availability switch
    {
        RootAvailability.Available => "MediaAvailable",
        RootAvailability.AccessDenied => "RootAccessDenied",
        _ => "MediaUnavailable",
    };
}

public sealed class RootOnboardingViewModel : INotifyPropertyChanged
{
    private readonly AddLibraryRoot _addLibraryRoot;
    private readonly RemoveLibraryRoot? _removeLibraryRoot;
    private readonly ILibraryRootRepository? _roots;
    private string _path = string.Empty;
    private RootKind _selectedKind = RootKind.Local;
    private ScanPolicy _selectedScanPolicy = ScanPolicy.Startup | ScanPolicy.Manual;
    private bool _initialScanConsentRequired;
    private bool _canStartInitialScan;
    private string? _failureKey;
    private IReadOnlyList<LibraryRootRowViewModel> _rootRows = [];
    private LibraryRootRowViewModel? _pendingRemoval;
    private LibraryRootId? _removedRootId;

    public RootOnboardingViewModel(
        AddLibraryRoot addLibraryRoot,
        RemoveLibraryRoot? removeLibraryRoot = null,
        ILibraryRootRepository? roots = null)
    {
        _addLibraryRoot = addLibraryRoot ?? throw new ArgumentNullException(nameof(addLibraryRoot));
        _removeLibraryRoot = removeLibraryRoot;
        _roots = roots;
        SelectKindCommand = new RelayCommand(parameter =>
        {
            if (parameter is RootKind kind)
            {
                SelectedKind = kind;
            }
        });
        AddRootCommand = new AsyncRelayCommand(() => AddAsync(CancellationToken.None));
        GrantInitialScanConsentCommand = new RelayCommand(_ => GrantInitialScanConsent());
        RequestRemoveCommand = new RelayCommand(parameter =>
        {
            if (parameter is LibraryRootRowViewModel row)
            {
                PendingRemoval = row;
            }
        });
        CancelRemoveCommand = new RelayCommand(_ => PendingRemoval = null);
        ConfirmRemoveCommand = new AsyncRelayCommand(() => ConfirmRemoveAsync(CancellationToken.None));
        BrowseFolderCommand = new AsyncRelayCommand(async () =>
        {
            if (FolderPicker is { } pick
                && await pick(CancellationToken.None).ConfigureAwait(true) is { } chosen)
            {
                Path = chosen;
            }
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Detects a folder's kind from its path, when the host wires one: UNC from the prefix, USB
    /// from the drive. Null in tests and previews, where the three kind pills stand in.
    /// </summary>
    /// <remarks>
    /// A delegate rather than a port, which is the shape the file pickers already have: the
    /// composition decides once, and the model never learns where the answer comes from.
    /// </remarks>
    public Func<string, RootKind>? KindDetector { get; set; }

    /// <summary>
    /// Asks the host for a folder, when the host has a dialog to ask with. Null when it does not —
    /// and then the Browse button is absent rather than disabled, because offering a dialog no run
    /// can open would be offering something that cannot occur.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? FolderPicker { get; set; }

    public bool HasKindDetector => KindDetector is not null;

    public bool HasFolderPicker => FolderPicker is not null;

    /// <summary>The detected kind and its consequence, as resource keys the dialog paints.</summary>
    public string DetectedKindKey => SelectedKind switch
    {
        RootKind.Usb => "RootKindUsb",
        RootKind.Unc => "RootKindUnc",
        _ => "RootKindLocal",
    };

    public string DetectedKindHintKey => SelectedKind switch
    {
        RootKind.Usb => "RootKindUsbHint",
        RootKind.Unc => "RootKindUncHint",
        _ => "RootKindLocalHint",
    };

    public string Path
    {
        get => _path;
        set
        {
            if (SetField(ref _path, value ?? string.Empty) && KindDetector is { } detect
                && !string.IsNullOrWhiteSpace(_path))
            {
                // The kind follows the path as it is typed or picked, which is the dialog's
                // grammar: the type is detected, not chosen. Where no detector is wired the three
                // pills stay in charge and this does nothing.
                SelectedKind = detect(_path);
            }
        }
    }

    /// <summary>
    /// Which of the three kinds the next folder will be added as.
    /// </summary>
    /// <remarks>
    /// The three buttons that set this painted nothing back: no view read the property, so the kind
    /// was chosen and the screen looked identical whichever one was pressed - and it starts at
    /// <c>Local</c>, so there was never even a moment with nothing selected to make the absence
    /// obvious. The three cues below are what the buttons now show, the same way the theme and
    /// language pills already do it.
    /// </remarks>
    public RootKind SelectedKind
    {
        get => _selectedKind;
        set
        {
            SetField(ref _selectedKind, value);
            OnPropertyChanged(nameof(LocalStateCue));
            OnPropertyChanged(nameof(UsbStateCue));
            OnPropertyChanged(nameof(UncStateCue));
            OnPropertyChanged(nameof(DetectedKindKey));
            OnPropertyChanged(nameof(DetectedKindHintKey));
        }
    }

    public string LocalStateCue => StateCue(RootKind.Local);

    public string UsbStateCue => StateCue(RootKind.Usb);

    public string UncStateCue => StateCue(RootKind.Unc);

    public ScanPolicy SelectedScanPolicy
    {
        get => _selectedScanPolicy;
        set => SetField(ref _selectedScanPolicy, value);
    }

    public LibraryRoot? AddedRoot { get; private set; }

    public bool InitialScanConsentRequired
    {
        get => _initialScanConsentRequired;
        private set => SetField(ref _initialScanConsentRequired, value);
    }

    public bool CanStartInitialScan
    {
        get => _canStartInitialScan;
        private set => SetField(ref _canStartInitialScan, value);
    }

    public ICommand SelectKindCommand { get; }

    public ICommand AddRootCommand { get; }

    /// <summary>
    /// Opens the host's folder dialog and puts the answer in <see cref="Path"/>. A cancelled dialog
    /// answers null and changes nothing, exactly like every other picker in this application.
    /// </summary>
    public ICommand BrowseFolderCommand { get; }

    public ICommand GrantInitialScanConsentCommand { get; }

    /// <summary>Puts a folder on the confirmation step; nothing is touched until it is confirmed.</summary>
    public ICommand RequestRemoveCommand { get; }

    public ICommand ConfirmRemoveCommand { get; }

    public ICommand CancelRemoveCommand { get; }

    /// <summary>The folders the library catalogs today, so this surface is also where one leaves.</summary>
    public IReadOnlyList<LibraryRootRowViewModel> Roots
    {
        get => _rootRows;
        private set
        {
            _rootRows = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasRoots));
            OnPropertyChanged(nameof(HasNoRoots));
        }
    }

    public bool HasRoots => Roots.Count > 0;

    /// <summary>
    /// True while the catalog holds no folder at all, which is how this screen starts.
    /// </summary>
    /// <remarks>
    /// SURFACES lists four forms for this view and "no roots" is the first of them. It was also the
    /// only one with nothing to paint: with an empty list the heading and the rows were simply
    /// absent, and the most common state of the first-run screen said nothing whatsoever.
    /// </remarks>
    public bool HasNoRoots => Roots.Count == 0;

    /// <summary>The folder whose removal is awaiting a person's confirmation, if any.</summary>
    public LibraryRootRowViewModel? PendingRemoval
    {
        get => _pendingRemoval;
        private set
        {
            _pendingRemoval = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsConfirmingRemoval));
            OnPropertyChanged(nameof(PendingRemovalPath));
        }
    }

    public bool IsConfirmingRemoval => PendingRemoval is not null;

    public string PendingRemovalPath => PendingRemoval?.Path ?? string.Empty;

    /// <summary>The last root a confirmation actually removed, so the shell can reload the catalog.</summary>
    public LibraryRootId? RemovedRootId
    {
        get => _removedRootId;
        private set
        {
            _removedRootId = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Which refusal is on screen, as a resource key, or nothing when the last attempt worked.</summary>
    public string? FailureKey
    {
        get => _failureKey;
        private set => SetField(ref _failureKey, value);
    }

    public bool HasFailure => FailureKey is not null;

    /// <summary>
    /// Adds the folder, or says why it cannot be added.
    /// <para>
    /// A refusal is answered on the screen rather than thrown. Letting it out of here reaches the
    /// dispatcher, and on Windows that ends the process: a folder added twice used to close the
    /// application instead of explaining itself.
    /// </para>
    /// </summary>
    public async Task AddAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            AddedRoot = await _addLibraryRoot.ExecuteAsync(
                new AddLibraryRootCommand(Path, SelectedKind, SelectedScanPolicy),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            AddedRoot = null;
            OnPropertyChanged(nameof(AddedRoot));
            InitialScanConsentRequired = false;
            CanStartInitialScan = false;
            FailureKey = Describe(exception);
            OnPropertyChanged(nameof(HasFailure));
            return;
        }

        OnPropertyChanged(nameof(AddedRoot));
        FailureKey = null;
        OnPropertyChanged(nameof(HasFailure));
        InitialScanConsentRequired = true;
        CanStartInitialScan = false;

        // The list follows the catalogue it describes: without this, a folder added stayed off its
        // own list — and off the first-run test that decides whether the form is still needed —
        // until somebody navigated away and back.
        await RefreshRootsAsync(cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Turns a refusal into the sentence the screen shows. The three cases are the three a person can
    /// actually cause: the same folder twice, one inside another, and a path Windows will not take.
    /// </summary>
    private static string Describe(Exception exception) => exception switch
    {
        InvalidOperationException when exception.Message.Contains("Duplicate", StringComparison.Ordinal) =>
            "RootAddDuplicate",
        InvalidOperationException when exception.Message.Contains("Nested", StringComparison.Ordinal) =>
            "RootAddNested",
        _ => "RootAddInvalidPath",
    };

    /// <summary>
    /// Puts the form back to nothing, so the next folder is typed into an empty box.
    /// </summary>
    /// <remarks>
    /// Three leftovers, and each one was a way for this screen to answer for something a person did
    /// not do. Nothing emptied <see cref="Path"/> after a folder was accepted, so the folder just
    /// added stayed typed in and pressing Add again said "it is already in the library". A refusal
    /// stayed on screen until the next attempt <em>succeeded</em>, so a rejected path kept explaining
    /// itself over a box that had since been changed. And a removal somebody walked away from was
    /// still waiting to be confirmed on the way back.
    /// <para>
    /// The scan consent is deliberately left alone: it is a question about a folder already saved,
    /// and dropping it would silently cancel the first scan of somebody's library.
    /// </para>
    /// </remarks>
    public void BeginAdd()
    {
        Path = string.Empty;
        SelectedKind = RootKind.Local;
        PendingRemoval = null;
        FailureKey = null;
        OnPropertyChanged(nameof(HasFailure));
    }

    /// <summary>Reads the folders the catalog holds right now; without the reader there is no list.</summary>
    public async Task RefreshRootsAsync(CancellationToken cancellationToken = default)
    {
        if (_roots is null)
        {
            return;
        }

        var listed = await _roots.ListAsync(cancellationToken).ConfigureAwait(true);
        Roots = [.. listed.Select(root => new LibraryRootRowViewModel(root))];
    }

    /// <summary>
    /// Removes the folder a person confirmed. Only the catalog forgets it: no video on disk is
    /// touched, and adding the folder again catalogs it anew — which is what the confirmation says.
    /// </summary>
    public async Task ConfirmRemoveAsync(CancellationToken cancellationToken = default)
    {
        if (_removeLibraryRoot is null || PendingRemoval is not { } row)
        {
            return;
        }

        await _removeLibraryRoot
            .ExecuteAsync(new RemoveLibraryRootCommand(row.Id), cancellationToken)
            .ConfigureAwait(true);
        PendingRemoval = null;
        RemovedRootId = row.Id;
        await RefreshRootsAsync(cancellationToken).ConfigureAwait(true);
    }

    public void GrantInitialScanConsent()
    {
        if (AddedRoot is null)
        {
            return;
        }

        InitialScanConsentRequired = false;
        CanStartInitialScan = true;
    }

    /// <summary>The circle this repository uses for "chosen", and the one it uses for "not".</summary>
    private string StateCue(RootKind kind) => SelectedKind == kind ? "●" : "○";

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

    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
