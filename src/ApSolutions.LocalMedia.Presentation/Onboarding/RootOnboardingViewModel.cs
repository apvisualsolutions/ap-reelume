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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path
    {
        get => _path;
        set => SetField(ref _path, value ?? string.Empty);
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
