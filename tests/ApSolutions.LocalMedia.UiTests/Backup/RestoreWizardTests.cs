// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Domain.Backup;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Backup;

/// <summary>
/// The restore wizard. It shows what a restore would do before it does anything, and it refuses to offer
/// the confirmation while the preview says the archive cannot be restored.
/// </summary>
public sealed class RestoreWizardTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_wizard_opens_with_nothing_chosen_and_nothing_to_confirm()
    {
        var wizard = CreateWizard();

        Assert.False(wizard.HasPreview);
        Assert.False(wizard.CanRestore);
        Assert.False(wizard.ConfirmCommand.CanExecute(null));
        Assert.Empty(wizard.Roots);
        Assert.Empty(wizard.Findings);
        Assert.Equal("RestoreStatusIdle", wizard.StatusKey);
    }

    [Fact]
    public async Task Choosing_an_archive_shows_what_the_restore_would_change_without_changing_it()
    {
        var restored = false;
        var wizard = CreateWizard(
            preview: (_, _, _) => Task.FromResult(Preview(
                [Decision("D:\\media", "D:\\media", RootRemapStatus.Missing)],
                findings: [],
                mediaFiles: 42,
                pathChanges: 0)),
            restore: (_, _, _, _) =>
            {
                restored = true;
                return Task.FromResult(new RestoreResult(true, Preview([], [], 0, 0), null, null));
            });

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.True(wizard.HasPreview);
        Assert.True(wizard.CanRestore);
        Assert.Equal(42, wizard.MediaFileCount);
        Assert.Equal(0, wizard.PathChangeCount);
        Assert.Equal("RestoreStatusPreviewed", wizard.StatusKey);
        var row = Assert.Single(wizard.Roots);
        Assert.Equal("D:\\media", row.OldPath);
        Assert.Equal("RestoreRootMissing", row.StatusKey);
        Assert.False(restored);
    }

    [Fact]
    public async Task An_archive_the_preview_refuses_offers_no_confirmation_and_says_why()
    {
        var wizard = CreateWizard(preview: (_, _, _) => Task.FromResult(Preview(
            [],
            [new RestoreFinding(RestoreFindingKind.HashMismatch, "library.db")],
            mediaFiles: 0,
            pathChanges: 0)));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.True(wizard.HasPreview);
        Assert.False(wizard.CanRestore);
        Assert.False(wizard.ConfirmCommand.CanExecute(null));
        Assert.Equal("RestoreStatusRefused", wizard.StatusKey);
        Assert.Equal(["RestoreFindingHashMismatch"], wizard.Findings.Select(finding => finding.MessageKey));
    }

    [Fact]
    public async Task Declining_the_file_dialog_leaves_the_wizard_where_it_was()
    {
        var previewed = false;
        var wizard = CreateWizard(
            archive: null,
            preview: (_, _, _) =>
            {
                previewed = true;
                return Task.FromResult(Preview([], [], 0, 0));
            });

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.False(previewed);
        Assert.False(wizard.HasPreview);
        Assert.Equal("RestoreStatusIdle", wizard.StatusKey);
    }

    [Fact]
    public async Task Editing_a_new_folder_reruns_the_preview_with_that_remap()
    {
        var seen = new List<IReadOnlyList<RootRemap>>();
        var wizard = CreateWizard(preview: (_, remaps, _) =>
        {
            seen.Add(remaps);
            return Task.FromResult(Preview(
                [Decision("D:\\media", remaps.Count == 0 ? "D:\\media" : remaps[0].NewPath,
                    remaps.Count == 0 ? RootRemapStatus.Missing : RootRemapStatus.Remapped)],
                [],
                mediaFiles: 3,
                pathChanges: remaps.Count == 0 ? 0 : 3));
        });

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        wizard.Roots[0].NewPath = "F:\\library";
        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, seen.Count);
        Assert.Empty(seen[0]);
        Assert.Equal("D:\\media", Assert.Single(seen[1]).OldPath);
        Assert.Equal("F:\\library", seen[1][0].NewPath);
        Assert.Equal(3, wizard.PathChangeCount);
        Assert.Equal("RestoreRootRemapped", wizard.Roots[0].StatusKey);
    }

    [Fact]
    public async Task Confirming_restores_and_reports_where_the_replaced_database_was_kept()
    {
        var wizard = CreateWizard(
            preview: (_, _, _) => Task.FromResult(Preview([], [], 7, 0)),
            restore: (_, _, _, _) => Task.FromResult(new RestoreResult(
                true,
                Preview([], [], 7, 0),
                "D:\\data\\library.db.pre-restore-20260803T120000Z.bak",
                null)));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        await wizard.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal("RestoreStatusRestored", wizard.StatusKey);
        Assert.Equal("library.db.pre-restore-20260803T120000Z.bak", wizard.PreservedDatabaseName);
        Assert.DoesNotContain("data", wizard.PreservedDatabaseName!, StringComparison.OrdinalIgnoreCase);
        Assert.False(wizard.IsRunning);
    }

    [Fact]
    public async Task A_restore_that_fails_says_so_and_leaves_the_wizard_usable()
    {
        var wizard = CreateWizard(
            preview: (_, _, _) => Task.FromResult(Preview([], [], 1, 0)),
            restore: (_, _, _, _) => Task.FromResult(new RestoreResult(
                false,
                Preview([], [new RestoreFinding(RestoreFindingKind.DatabaseUnreadable, "library.db")], 1, 0),
                null,
                "the staged database could not be opened")));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        await wizard.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal("RestoreStatusFailed", wizard.StatusKey);
        Assert.Null(wizard.PreservedDatabaseName);
        Assert.Equal(["RestoreFindingDatabaseUnreadable"], wizard.Findings.Select(finding => finding.MessageKey));
    }

    [Fact]
    public async Task Confirming_without_a_preview_does_nothing_at_all()
    {
        var attempts = 0;
        var wizard = CreateWizard(restore: (_, _, _, _) =>
        {
            attempts++;
            return Task.FromResult(new RestoreResult(true, Preview([], [], 0, 0), null, null));
        });

        await wizard.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, attempts);
        Assert.Equal("RestoreStatusIdle", wizard.StatusKey);
    }

    [Fact]
    public async Task A_conflict_blocks_the_restore_and_names_both_rows()
    {
        var wizard = CreateWizard(preview: (_, _, _) => Task.FromResult(Preview(
            [
                Decision("D:\\media", "F:\\one", RootRemapStatus.Conflict),
                Decision("E:\\archive", "F:\\one", RootRemapStatus.Conflict),
            ],
            [new RestoreFinding(RestoreFindingKind.RootConflict, "F:\\one")],
            mediaFiles: 9,
            pathChanges: 9)));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.False(wizard.CanRestore);
        Assert.Equal(2, wizard.Roots.Count);
        Assert.All(wizard.Roots, row => Assert.Equal("RestoreRootConflict", row.StatusKey));
        Assert.All(wizard.Roots, row => Assert.True(row.IsBlocking));
    }

    [Fact]
    public async Task The_commands_do_what_the_methods_do()
    {
        var previews = 0;
        var restores = 0;
        using var finished = new SemaphoreSlim(0, 2);
        var wizard = CreateWizard(
            preview: (_, _, _) =>
            {
                Interlocked.Increment(ref previews);
                finished.Release();
                return Task.FromResult(Preview([], [], 4, 0));
            },
            restore: (_, _, _, _) =>
            {
                Interlocked.Increment(ref restores);
                finished.Release();
                return Task.FromResult(new RestoreResult(true, Preview([], [], 4, 0), "kept.bak", null));
            });

        wizard.ChooseArchiveCommand.Execute(null);
        await finished.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(wizard.ConfirmCommand.CanExecute(null));
        wizard.ConfirmCommand.Execute(null);
        await finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, previews);
        Assert.Equal(1, restores);
    }

    [Fact]
    public async Task A_second_look_asked_for_while_the_first_is_running_never_starts()
    {
        var previews = 0;
        using var release = new SemaphoreSlim(0, 1);
        RestoreWizardViewModel? wizard = null;
        wizard = CreateWizard(preview: async (_, _, cancellationToken) =>
        {
            if (Interlocked.Increment(ref previews) == 1)
            {
                await wizard!.PreviewAsync(cancellationToken);
                await release.WaitAsync(cancellationToken);
            }

            return Preview([], [], 1, 0);
        });

        var first = wizard.PreviewAsync(TestContext.Current.CancellationToken);
        release.Release();
        await first;

        Assert.Equal(1, previews);
    }

    [Fact]
    public async Task An_archive_that_cannot_be_opened_at_all_is_reported_as_a_refusal()
    {
        var wizard = CreateWizard(preview: (_, _, _) =>
            Task.FromException<RestorePreview>(new IOException("the file went away")));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.True(wizard.HasPreview);
        Assert.False(wizard.CanRestore);
        Assert.Equal("RestoreStatusRefused", wizard.StatusKey);
        Assert.Equal(
            ["RestoreFindingUnreadableArchive"],
            wizard.Findings.Select(finding => finding.MessageKey));
    }

    [Fact]
    public async Task A_restore_that_throws_is_reported_without_taking_the_wizard_down()
    {
        var wizard = CreateWizard(
            preview: (_, _, _) => Task.FromResult(Preview([], [], 2, 0)),
            restore: (_, _, _, _) =>
                Task.FromException<RestoreResult>(new IOException("the disk went away")));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        await wizard.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal("RestoreStatusFailed", wizard.StatusKey);
        Assert.Null(wizard.PreservedDatabaseName);
        Assert.False(wizard.IsRunning);
        Assert.Contains(
            wizard.Findings,
            finding => finding.MessageKey == "RestoreFindingUnreadableArchive");
    }

    [Fact]
    public async Task The_wizard_follows_the_progress_the_restore_reports()
    {
        RestoreWizardViewModel? wizard = null;
        wizard = CreateWizard(
            preview: (_, _, _) => Task.FromResult(Preview([], [], 3, 0)),
            restore: (_, _, progress, _) =>
            {
                progress.Report(new BackupProgress(BackupStage.Snapshot, 1, 3));
                Assert.Equal(1, wizard!.Completed);
                progress.Report(new BackupProgress(BackupStage.Publish, 3, 3));
                return Task.FromResult(new RestoreResult(true, Preview([], [], 3, 0), "kept.bak", null));
            });

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        await wizard.ConfirmAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, wizard.Completed);
        Assert.Equal(3, wizard.Total);
    }

    [Fact]
    public async Task A_root_that_disappears_between_two_looks_stops_being_shown()
    {
        var round = 0;
        var wizard = CreateWizard(preview: (_, _, _) => Task.FromResult(round++ == 0
            ? Preview(
                [
                    Decision("D:\\media", "D:\\media", RootRemapStatus.Missing),
                    Decision("E:\\archive", "E:\\archive", RootRemapStatus.Missing),
                ],
                [],
                5,
                0)
            : Preview([Decision("D:\\media", "D:\\media", RootRemapStatus.Missing)], [], 5, 0)));

        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, wizard.Roots.Count);
        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.Equal("D:\\media", Assert.Single(wizard.Roots).OldPath);
    }

    /// <summary>
    /// A button bound to a command asks once and waits to be told. Without the event the confirmation
    /// stays disabled forever and a valid restore can never be started from the application at all.
    /// </summary>
    [Fact]
    public async Task A_preview_that_can_be_restored_tells_the_surface_the_confirmation_woke_up()
    {
        var wizard = CreateWizard(preview: (_, _, _) => Task.FromResult(Preview([], [], 3, 0)));
        var notifications = 0;
        wizard.ConfirmCommand.CanExecuteChanged += (_, _) => notifications++;

        Assert.False(wizard.ConfirmCommand.CanExecute(null));
        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        Assert.True(wizard.ConfirmCommand.CanExecute(null));
        Assert.True(notifications > 0, "The surface was never told the confirmation became possible.");
    }

    [Fact]
    public void Every_visible_string_on_the_wizard_comes_from_the_resource_dictionary()
    {
        var presentationRoot = RepositoryLayout.PathFromRoot("src", "ApSolutions.LocalMedia.Presentation");
        var view = XDocument.Load(Path.Combine(presentationRoot, "Backup", "RestoreWizardView.axaml"));
        var spanish = LoadResourceKeys(Path.Combine(presentationRoot, "Resources", "Strings.es.axaml"));

        var literals = view.Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "Header" or "Watermark")
            .Select(attribute => attribute.Value)
            .Where(value => !value.StartsWith('{'))
            .ToArray();

        Assert.Empty(literals);
        foreach (var key in new[]
        {
            "RestoreTitle",
            "RestoreDescription",
            "RestoreChooseArchiveLabel",
            "RestoreConfirmLabel",
            "RestoreStatusIdle",
            "RestoreStatusPreviewed",
            "RestoreStatusRefused",
            "RestoreStatusRestored",
            "RestoreStatusFailed",
            "RestoreRootUnchanged",
            "RestoreRootRemapped",
            "RestoreRootMissing",
            "RestoreRootConflict",
            "RestoreFindingHashMismatch",
            "RestoreFindingRootConflict",
            "RestorePreservedNotice",
        })
        {
            Assert.Contains(key, spanish);
        }
    }

    [Fact]
    public void Every_finding_the_workflow_can_produce_has_a_message_of_its_own()
    {
        var spanish = LoadResourceKeys(Path.Combine(
            RepositoryLayout.PathFromRoot("src", "ApSolutions.LocalMedia.Presentation"),
            "Resources",
            "Strings.es.axaml"));

        foreach (var kind in Enum.GetValues<RestoreFindingKind>())
        {
            Assert.Contains($"RestoreFinding{kind}", spanish);
        }
    }

    /// <summary>
    /// Only the root that needs a folder gets a box to type one into.
    /// </summary>
    /// <remarks>
    /// Every row had a text box, including the ones whose folder is exactly where the backup left it,
    /// so a restore of five roots offered five invitations to change something and four of them were
    /// answers to a question nobody asked. A missing folder is a fact rather than a mistake — the
    /// domain says so where the status is defined — and it is the one that needs an answer.
    /// </remarks>
    [AvaloniaFact]
    public async Task Only_the_root_that_needs_a_folder_offers_a_box_to_type_one()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var wizard = CreateWizard(preview: (_, _, _) => Task.FromResult(Preview(
            [
                new RootRemapDecision(@"R:\mediailms", @"R:\mediailms", RootRemapStatus.Unchanged),
                new RootRemapDecision(@"Q:\gone\shows", @"Q:\gone\shows", RootRemapStatus.Missing),
            ],
            [],
            10,
            0)));
        await wizard.PreviewAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, wizard.Roots.Count);

        var view = new RestoreWizardView { DataContext = wizard };
        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var boxes = view.GetVisualDescendants()
            .OfType<TextBox>()
            .Where(box => box.IsEffectivelyVisible)
            .ToArray();
        Assert.Single(boxes);

        window.Close();
    }

    /// <summary>
    /// The old paths are fixed-width and keep their end, which is what tells two of them apart.
    /// </summary>
    [AvaloniaFact]
    public async Task The_old_paths_are_fixed_width_and_keep_the_end_that_tells_them_apart()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var wizard = CreateWizard(preview: (_, _, _) => Task.FromResult(Preview(
            [new RootRemapDecision(@"Q:\gone\shows", @"Q:\gone\shows", RootRemapStatus.Missing)],
            [],
            10,
            0)));
        await wizard.PreviewAsync(TestContext.Current.CancellationToken);

        var view = new RestoreWizardView { DataContext = wizard };
        var window = new Window { Width = 900, Height = 900, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var application = Avalonia.Application.Current!;
        Assert.True(application.TryFindResource("FontFamilyMono", out var mono));
        var family = Assert.IsType<Avalonia.Media.FontFamily>(mono);

        var path = Assert.Single(
            view.GetVisualDescendants().OfType<TextBlock>(),
            block => (block.Text ?? string.Empty).Contains("gone", StringComparison.Ordinal));
        Assert.Equal(family.Name, path.FontFamily.Name);
        Assert.Equal(Avalonia.Media.TextTrimming.PathSegmentEllipsis, path.TextTrimming);

        window.Close();
    }

    /// <summary>
    /// Typing a folder says "reassigned" straight away, rather than still saying "missing".
    /// </summary>
    /// <remarks>
    /// The status came from the last dry run, so a row kept saying the folder was missing while
    /// somebody was looking at the folder they had just typed. A conflict is not masked this way: it
    /// is the one status that blocks the restore, and it stays what it is until a run says otherwise.
    /// </remarks>
    [Fact]
    public void Typing_a_folder_says_reassigned_and_a_conflict_still_says_conflict()
    {
        var missing = new RootRemapRowViewModel(
            new RootRemapDecision(@"Q:\gone\shows", @"Q:\gone\shows", RootRemapStatus.Missing));
        Assert.Equal("RestoreRootMissing", missing.StatusKey);
        Assert.True(missing.NeedsFolder);

        missing.NewPath = @"S:\shows";
        Assert.Equal("RestoreRootRemapped", missing.StatusKey);

        var unchanged = new RootRemapRowViewModel(
            new RootRemapDecision(@"R:\media", @"R:\media", RootRemapStatus.Unchanged));
        Assert.False(unchanged.NeedsFolder);

        var conflict = new RootRemapRowViewModel(
            new RootRemapDecision(@"R:\media", @"S:\other", RootRemapStatus.Conflict));
        Assert.Equal("RestoreRootConflict", conflict.StatusKey);
        Assert.True(conflict.NeedsFolder);
    }

    private static RestorePreview Preview(
        IReadOnlyList<RootRemapDecision> roots,
        IReadOnlyList<RestoreFinding> findings,
        int mediaFiles,
        int pathChanges) =>
        new(
            new BackupManifest(BackupManifest.CurrentFormatVersion, "1.0.0", Noon, "0", null, [], []),
            findings,
            roots,
            RequiredBytes: 1024,
            AvailableBytes: 4096,
            MediaFileCount: mediaFiles,
            PathChangeCount: pathChanges);

    private static RootRemapDecision Decision(string oldPath, string newPath, RootRemapStatus status) =>
        new(oldPath, newPath, status);

    private static RestoreWizardViewModel CreateWizard(
        string? archive = "D:\\exports\\library.zip",
        Func<string, IReadOnlyList<RootRemap>, CancellationToken, Task<RestorePreview>>? preview = null,
        Func<string, IReadOnlyList<RootRemap>, IProgress<BackupProgress>, CancellationToken, Task<RestoreResult>>? restore = null) =>
        new(
            preview ?? ((_, _, _) => Task.FromResult(Preview([], [], 0, 0))),
            restore ?? ((_, _, _, _) => Task.FromResult(new RestoreResult(true, Preview([], [], 0, 0), null, null))),
            _ => Task.FromResult(archive));

    private static HashSet<string> LoadResourceKeys(string path)
    {
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        return [.. XDocument.Load(path)
            .Descendants()
            .Select(element => element.Attribute(xNamespace + "Key")?.Value)
            .Where(value => value is not null)
            .Select(value => value!)];
    }
}
