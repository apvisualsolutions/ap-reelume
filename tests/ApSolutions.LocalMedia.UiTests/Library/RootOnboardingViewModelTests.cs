// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Onboarding;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// Adding a folder, including the ways it can go wrong.
/// <para>
/// A refusal has to arrive as a sentence on the screen. Letting it escape the command reaches the
/// dispatcher, and on Windows that ends the process: walking the real application by hand is how a
/// folder added twice was found to close the application instead of explaining itself.
/// </para>
/// </summary>
public sealed class RootOnboardingViewModelTests
{
    [Fact]
    public async Task A_folder_that_is_already_in_the_library_is_refused_in_words()
    {
        var viewModel = Create(new StubRoots(Existing("R:\\media")));
        viewModel.Path = "R:\\media";

        await viewModel.AddAsync(TestContext.Current.CancellationToken);

        Assert.Null(viewModel.AddedRoot);
        Assert.True(viewModel.HasFailure);
        Assert.Equal("RootAddDuplicate", viewModel.FailureKey);
        Assert.False(viewModel.InitialScanConsentRequired);
    }

    [Fact]
    public async Task A_folder_inside_one_the_library_already_has_is_refused_in_words()
    {
        var viewModel = Create(new StubRoots(Existing("R:\\media")));
        viewModel.Path = "R:\\media\\subfolder";

        await viewModel.AddAsync(TestContext.Current.CancellationToken);

        Assert.Null(viewModel.AddedRoot);
        Assert.Equal("RootAddNested", viewModel.FailureKey);
    }

    [Fact]
    public async Task A_path_the_platform_will_not_accept_is_refused_in_words()
    {
        var viewModel = new RootOnboardingViewModel(
            new AddLibraryRoot(new StubRoots(), new RejectingNormalizer()));
        viewModel.Path = "no-such-thing";

        await viewModel.AddAsync(TestContext.Current.CancellationToken);

        Assert.Null(viewModel.AddedRoot);
        Assert.Equal("RootAddInvalidPath", viewModel.FailureKey);
    }

    [Fact]
    public async Task A_folder_that_can_be_added_clears_whatever_the_last_attempt_said()
    {
        var roots = new StubRoots(Existing("R:\\media"));
        var viewModel = Create(roots);
        viewModel.Path = "R:\\media";
        await viewModel.AddAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.HasFailure);

        viewModel.Path = "R:\\other";
        await viewModel.AddAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(viewModel.AddedRoot);
        Assert.False(viewModel.HasFailure);
        Assert.True(viewModel.InitialScanConsentRequired);
    }

    /// <summary>
    /// The button is what a person actually presses, and it is the path that used to end the process.
    /// Pressing it on a duplicate has to leave the view model standing.
    /// </summary>
    [Fact]
    public async Task Pressing_the_button_on_a_duplicate_leaves_the_application_standing()
    {
        var viewModel = Create(new StubRoots(Existing("R:\\media")));
        viewModel.Path = "R:\\media";
        var failures = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(RootOnboardingViewModel.FailureKey))
            {
                failures++;
            }
        };

        viewModel.AddRootCommand.Execute(null);
        for (var attempt = 0; attempt < 100 && failures == 0; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, failures);
        Assert.True(viewModel.HasFailure);
    }

    [Fact]
    public void Granting_consent_before_a_folder_exists_changes_nothing()
    {
        var viewModel = Create(new StubRoots());

        viewModel.GrantInitialScanConsent();

        Assert.False(viewModel.CanStartInitialScan);
    }

    /// <summary>
    /// Removing a folder is a confirmed decision that only touches the catalog (LIB-A01): asking
    /// removes nothing, confirming removes the root alone, and cancelling leaves everything be.
    /// </summary>
    [Fact]
    public async Task The_folder_list_shows_what_the_catalog_holds()
    {
        var viewModel = CreateWithRemoval(new StubRoots(Existing("R:\\media"), Existing("R:\\series")));

        await viewModel.RefreshRootsAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasRoots);
        Assert.Equal(2, viewModel.Roots.Count);
        Assert.Contains(viewModel.Roots, row => row.Path == "R:\\media");
    }

    [Fact]
    public async Task Asking_to_remove_touches_nothing_until_it_is_confirmed()
    {
        var roots = new StubRoots(Existing("R:\\media"));
        var viewModel = CreateWithRemoval(roots);
        await viewModel.RefreshRootsAsync(TestContext.Current.CancellationToken);
        var row = Assert.Single(viewModel.Roots);

        viewModel.RequestRemoveCommand.Execute(row);

        Assert.True(viewModel.IsConfirmingRemoval);
        Assert.Equal("R:\\media", viewModel.PendingRemovalPath);
        Assert.Single(await roots.ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Confirming_removes_the_folder_from_the_catalog_and_refreshes_the_list()
    {
        var roots = new StubRoots(Existing("R:\\media"));
        var viewModel = CreateWithRemoval(roots);
        await viewModel.RefreshRootsAsync(TestContext.Current.CancellationToken);
        var row = Assert.Single(viewModel.Roots);
        viewModel.RequestRemoveCommand.Execute(row);

        await viewModel.ConfirmRemoveAsync(TestContext.Current.CancellationToken);

        Assert.Empty(await roots.ListAsync(TestContext.Current.CancellationToken));
        Assert.False(viewModel.HasRoots);
        Assert.False(viewModel.IsConfirmingRemoval);
        Assert.Equal(row.Id, viewModel.RemovedRootId);
    }

    [Fact]
    public async Task Cancelling_leaves_the_folder_where_it_was()
    {
        var roots = new StubRoots(Existing("R:\\media"));
        var viewModel = CreateWithRemoval(roots);
        await viewModel.RefreshRootsAsync(TestContext.Current.CancellationToken);
        viewModel.RequestRemoveCommand.Execute(Assert.Single(viewModel.Roots));

        viewModel.CancelRemoveCommand.Execute(null);

        Assert.False(viewModel.IsConfirmingRemoval);
        Assert.Single(await roots.ListAsync(TestContext.Current.CancellationToken));
        Assert.Null(viewModel.RemovedRootId);
    }

    [Fact]
    public async Task Without_its_collaborators_the_folder_list_stays_absent()
    {
        var viewModel = Create(new StubRoots(Existing("R:\\media")));

        await viewModel.RefreshRootsAsync(TestContext.Current.CancellationToken);
        await viewModel.ConfirmRemoveAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.HasRoots);
        Assert.False(viewModel.IsConfirmingRemoval);
    }

    /// <summary>
    /// The dialog's grammar: the kind follows the path when a detector is wired, stands still when
    /// none is, and each answer carries its key and its consequence for the surface to paint.
    /// </summary>
    [Fact]
    public void The_kind_follows_the_path_only_where_a_detector_is_wired()
    {
        var undetected = Create(new StubRoots());
        Assert.False(undetected.HasKindDetector);
        undetected.Path = @"\\nas\cine";
        Assert.Equal(RootKind.Local, undetected.SelectedKind);

        var viewModel = Create(new StubRoots());
        viewModel.KindDetector = path => path.StartsWith(@"\\", StringComparison.Ordinal)
            ? RootKind.Unc
            : RootKind.Usb;
        Assert.True(viewModel.HasKindDetector);

        viewModel.Path = @"\\nas\cine";
        Assert.Equal(RootKind.Unc, viewModel.SelectedKind);
        Assert.Equal("RootKindUnc", viewModel.DetectedKindKey);
        Assert.Equal("RootKindUncHint", viewModel.DetectedKindHintKey);

        viewModel.Path = "E:\\peliculas";
        Assert.Equal(RootKind.Usb, viewModel.SelectedKind);
        Assert.Equal("RootKindUsb", viewModel.DetectedKindKey);
        Assert.Equal("RootKindUsbHint", viewModel.DetectedKindHintKey);

        // Emptying the box detects nothing: there is no path to read a kind from.
        viewModel.Path = string.Empty;
        Assert.Equal(RootKind.Usb, viewModel.SelectedKind);

        viewModel.SelectedKind = RootKind.Local;
        Assert.Equal("RootKindLocal", viewModel.DetectedKindKey);
        Assert.Equal("RootKindLocalHint", viewModel.DetectedKindHintKey);
    }

    /// <summary>
    /// Browse puts the picker's answer in the box and a cancelled picker changes nothing, exactly
    /// like every other picker in this application.
    /// </summary>
    [Fact]
    public async Task Browse_asks_the_picker_and_a_cancelled_picker_changes_nothing()
    {
        var absent = Create(new StubRoots());
        Assert.False(absent.HasFolderPicker);

        var viewModel = Create(new StubRoots());
        viewModel.FolderPicker = _ => Task.FromResult<string?>("R:\\chosen");
        Assert.True(viewModel.HasFolderPicker);

        viewModel.BrowseFolderCommand.Execute(null);
        for (var attempt = 0; attempt < 100 && viewModel.Path.Length == 0; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal("R:\\chosen", viewModel.Path);

        viewModel.FolderPicker = _ => Task.FromResult<string?>(null);
        viewModel.BrowseFolderCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal("R:\\chosen", viewModel.Path);
    }

    /// <summary>
    /// The list follows the catalogue it describes: a folder accepted lands on its own list without
    /// anybody navigating away and back — the house defect, in its list shape.
    /// </summary>
    [Fact]
    public async Task A_folder_accepted_reaches_the_list_without_a_second_ask()
    {
        var roots = new StubRoots();
        var viewModel = CreateWithRemoval(roots);
        Assert.True(viewModel.HasNoRoots);

        viewModel.Path = "R:\\media";
        await viewModel.AddAsync(TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasRoots);
        Assert.Contains(viewModel.Roots, row => row.Path == "R:\\media");
    }

    private static RootOnboardingViewModel Create(StubRoots roots) =>
        new(new AddLibraryRoot(roots, new PassThroughNormalizer()));

    private static RootOnboardingViewModel CreateWithRemoval(StubRoots roots) =>
        new(new AddLibraryRoot(roots, new PassThroughNormalizer()), new RemoveLibraryRoot(roots), roots);

    private static LibraryRoot Existing(string path) => new(
        new LibraryRootId(Guid.NewGuid()),
        path,
        RootKind.Local,
        RootAvailability.Available,
        ScanPolicy.Manual);

    private sealed class StubRoots(params LibraryRoot[] roots) : ILibraryRootRepository
    {
        private readonly List<LibraryRoot> _roots = [.. roots];

        public Task<LibraryRoot?> GetAsync(LibraryRootId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_roots.FirstOrDefault(root => root.Id == id));

        public Task<IReadOnlyList<LibraryRoot>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LibraryRoot>>(_roots);

        public Task AddAsync(LibraryRoot root, CancellationToken cancellationToken = default)
        {
            _roots.Add(root);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            LibraryRootId id,
            bool preserveCatalog = true,
            CancellationToken cancellationToken = default)
        {
            _roots.RemoveAll(root => root.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class PassThroughNormalizer : IPathNormalizer
    {
        public string NormalizeAndValidate(string path, RootKind kind) => path;
    }

    private sealed class RejectingNormalizer : IPathNormalizer
    {
        public string NormalizeAndValidate(string path, RootKind kind) =>
            throw new ArgumentException("The path is not a folder this platform accepts.", nameof(path));
    }
}
