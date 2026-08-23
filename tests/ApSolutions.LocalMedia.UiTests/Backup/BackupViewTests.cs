// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Backup;

/// <summary>
/// The copy and export surface. It shows what is happening, lets the run be stopped, and names the
/// destination by its file name only: a screen is no place for somebody's folders.
/// </summary>
public sealed class BackupViewTests
{
    private static readonly DateTimeOffset Noon = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_screen_opens_idle_with_nothing_in_flight()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.IsRunning);
        Assert.Equal("BackupStatusIdle", viewModel.StatusKey);
        Assert.Equal(0, viewModel.Completed);
        Assert.Equal(0, viewModel.Total);
        Assert.Null(viewModel.LastCopyName);
        Assert.Null(viewModel.LastArchiveName);
        Assert.False(viewModel.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task Creating_a_copy_walks_the_stages_and_finishes_with_the_copy_name()
    {
        var stages = new List<string>();
        BackupViewModel? viewModel = null;
        viewModel = CreateViewModel(onCopy: async (progress, _) =>
        {
            progress.Report(new BackupProgress(BackupStage.Snapshot, 0, 4));
            stages.Add(viewModel!.StageKey!);
            progress.Report(new BackupProgress(BackupStage.Publish, 4, 4));
            stages.Add(viewModel.StageKey!);
            await Task.Yield();
            return new BackupResult(
                new BackupCopy("D:\\data\\backups\\2026-08-03T120000Z", Noon, IsValid: true),
                Manifest(),
                []);
        });

        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsRunning);
        Assert.Equal("BackupStatusDone", viewModel.StatusKey);
        Assert.Equal(["BackupStageSnapshot", "BackupStagePublish"], stages);
        Assert.Equal("2026-08-03T120000Z", viewModel.LastCopyName);
        Assert.Equal(4, viewModel.Completed);
        Assert.Equal(4, viewModel.Total);
    }

    [Fact]
    public async Task Exporting_asks_for_a_destination_and_shows_only_its_file_name()
    {
        var viewModel = CreateViewModel(
            destination: "D:\\personal\\my library\\reelume-export.zip",
            onExport: (_, _, _) => Task.FromResult(new ExportResult(
                "D:\\personal\\my library\\reelume-export.zip",
                Manifest(),
                [BackupContentPolicy.DatabaseEntryName])));

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal("reelume-export.zip", viewModel.LastArchiveName);
        Assert.DoesNotContain("personal", viewModel.LastArchiveName!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("BackupStatusDone", viewModel.StatusKey);
    }

    [Fact]
    public async Task Declining_the_destination_dialog_leaves_the_screen_untouched()
    {
        var exported = false;
        var viewModel = CreateViewModel(
            destination: null,
            onExport: (_, _, _) =>
            {
                exported = true;
                return Task.FromResult(new ExportResult("ignored.zip", Manifest(), []));
            });

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.False(exported);
        Assert.Equal("BackupStatusIdle", viewModel.StatusKey);
        Assert.Null(viewModel.LastArchiveName);
    }

    [Fact]
    public async Task A_cancelled_run_returns_the_screen_to_a_state_that_can_start_again()
    {
        using var started = new SemaphoreSlim(0, 1);
        var viewModel = CreateViewModel(onCopy: async (_, cancellationToken) =>
        {
            started.Release();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new UnreachableException();
        });

        var run = viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);
        await started.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        viewModel.CancelCommand.Execute(null);
        await run;

        Assert.False(viewModel.IsRunning);
        Assert.Equal("BackupStatusCancelled", viewModel.StatusKey);
        Assert.True(viewModel.CreateCopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_run_that_runs_out_of_space_says_so_without_naming_a_path()
    {
        var viewModel = CreateViewModel(onCopy: (_, _) =>
            Task.FromException<BackupResult>(new InsufficientBackupSpaceException(4096, 1024)));

        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsRunning);
        Assert.Equal("BackupStatusNoSpace", viewModel.StatusKey);
        Assert.Null(viewModel.LastCopyName);
    }

    [Fact]
    public async Task Two_runs_never_overlap()
    {
        var runs = 0;
        using var started = new SemaphoreSlim(0, 1);
        using var release = new SemaphoreSlim(0, 1);
        var viewModel = CreateViewModel(onCopy: async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref runs);
            started.Release();
            await release.WaitAsync(cancellationToken);
            return new BackupResult(new BackupCopy("D:\\copies\\first", Noon, true), Manifest(), []);
        });

        var first = viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);
        await started.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(viewModel.CreateCopyCommand.CanExecute(null));
        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);
        release.Release();
        await first;

        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task An_export_that_fails_says_so_and_keeps_the_screen_usable()
    {
        var viewModel = CreateViewModel(onExport: (_, _, _) =>
            Task.FromException<ExportResult>(new IOException("the destination went away")));

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);

        Assert.Equal("BackupStatusFailed", viewModel.StatusKey);
        Assert.Null(viewModel.LastArchiveName);
        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Every_stage_the_workflow_can_report_has_a_key_of_its_own()
    {
        var seen = new List<string>();
        BackupViewModel? viewModel = null;
        viewModel = CreateViewModel(onCopy: (progress, _) =>
        {
            foreach (var stage in Enum.GetValues<BackupStage>())
            {
                progress.Report(new BackupProgress(stage, 1, 1));
                seen.Add(viewModel!.StageKey!);
            }

            return Task.FromResult(new BackupResult(
                new BackupCopy("D:\\copies\\copy", Noon, true),
                Manifest(),
                []));
        });

        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "BackupStageSnapshot",
                "BackupStagePreferences",
                "BackupStagePersonalArtwork",
                "BackupStageManifest",
                "BackupStagePublish",
                "BackupStageArchive",
            ],
            seen);
    }

    [Fact]
    public async Task The_commands_start_the_same_work_the_methods_do()
    {
        var copies = 0;
        var exports = 0;
        using var finished = new SemaphoreSlim(0, 2);
        var viewModel = CreateViewModel(
            onCopy: (_, _) =>
            {
                Interlocked.Increment(ref copies);
                finished.Release();
                return Task.FromResult(new BackupResult(
                    new BackupCopy("D:\\copies\\copy", Noon, true),
                    Manifest(),
                    []));
            },
            onExport: (path, _, _) =>
            {
                Interlocked.Increment(ref exports);
                finished.Release();
                return Task.FromResult(new ExportResult(path, Manifest(), []));
            });

        viewModel.CreateCopyCommand.Execute(null);
        await finished.WaitAsync(TestContext.Current.CancellationToken);
        viewModel.ExportCommand.Execute(null);
        await finished.WaitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, copies);
        Assert.Equal(1, exports);
    }

    [Fact]
    public async Task An_export_asked_for_while_a_copy_is_running_never_starts()
    {
        var exports = 0;
        using var release = new SemaphoreSlim(0, 1);
        BackupViewModel? viewModel = null;
        viewModel = CreateViewModel(
            onCopy: async (_, cancellationToken) =>
            {
                await release.WaitAsync(cancellationToken);
                return new BackupResult(new BackupCopy("D:\\copies\\copy", Noon, true), Manifest(), []);
            },
            onExport: (path, _, _) =>
            {
                Interlocked.Increment(ref exports);
                return Task.FromResult(new ExportResult(path, Manifest(), []));
            });

        var copy = viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        release.Release();
        await copy;

        Assert.Equal(0, exports);
    }

    /// <summary>
    /// §4's one addition to this view: where the active database lives, reachable without a
    /// failure. Handed in by the composition; a build without host paths paints no block.
    /// </summary>
    [Fact]
    public void The_database_path_handed_in_is_held_and_its_absence_paints_nothing()
    {
        var with = new BackupViewModel(
            (_, _) => Task.FromResult(new BackupResult(new BackupCopy("D:\\copies\\copy", Noon, true), Manifest(), [])),
            (path, _, _) => Task.FromResult(new ExportResult(path, Manifest(), [])),
            _ => Task.FromResult<string?>(null),
            "D:\\data\\library.db");
        Assert.True(with.HasDatabasePath);
        Assert.Equal("D:\\data\\library.db", with.DatabasePath);

        var without = new BackupViewModel(
            (_, _) => Task.FromResult(new BackupResult(new BackupCopy("D:\\copies\\copy", Noon, true), Manifest(), [])),
            (path, _, _) => Task.FromResult(new ExportResult(path, Manifest(), [])),
            _ => Task.FromResult<string?>(null));
        Assert.False(without.HasDatabasePath);
    }

    /// <summary>
    /// The empty history is the fresh installation's state and it is said in positive terms; the
    /// first finished copy takes its place, and the announcement is what the surface repaints by.
    /// </summary>
    [Fact]
    public async Task No_history_stands_until_the_first_copy_lands_and_says_so()
    {
        var viewModel = new BackupViewModel(
            (_, _) => Task.FromResult(new BackupResult(new BackupCopy("D:\\copies\\copy-2026", Noon, true), Manifest(), [])),
            (path, _, _) => Task.FromResult(new ExportResult(path, Manifest(), [])),
            _ => Task.FromResult<string?>(null));
        Assert.True(viewModel.HasNoHistory);

        var announced = new List<string>();
        viewModel.PropertyChanged += (_, e) => announced.Add(e.PropertyName ?? string.Empty);

        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);

        Assert.False(viewModel.HasNoHistory);
        Assert.Contains(nameof(viewModel.HasNoHistory), announced);
    }

    [Fact]
    public async Task A_copy_started_while_the_destination_dialog_is_open_wins_and_the_export_stands_down()
    {
        var exports = 0;
        using var release = new SemaphoreSlim(0, 1);
        BackupViewModel? viewModel = null;
        viewModel = new BackupViewModel(
            async (_, cancellationToken) =>
            {
                await release.WaitAsync(cancellationToken);
                return new BackupResult(new BackupCopy("D:\\copies\\copy", Noon, true), Manifest(), []);
            },
            (path, _, _) =>
            {
                Interlocked.Increment(ref exports);
                return Task.FromResult(new ExportResult(path, Manifest(), []));
            },
            cancellationToken =>
            {
                // The dialog is open while something else claims the screen, which is exactly the race
                // the guard after the dialog exists for.
                _ = viewModel!.CreateCopyAsync(cancellationToken);
                return Task.FromResult<string?>("D:\\exports\\library.zip");
            });

        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        release.Release();

        Assert.Equal(0, exports);
        Assert.True(viewModel.IsRunning || viewModel.StatusKey == "BackupStatusDone");
    }

    /// <summary>
    /// A button bound to a command asks once and waits to be told. Without the event the cancel button
    /// never becomes usable, however long a copy runs.
    /// </summary>
    [Fact]
    public async Task Starting_and_finishing_tell_the_surface_which_buttons_changed()
    {
        var cancelNotifications = 0;
        using var started = new SemaphoreSlim(0, 1);
        using var release = new SemaphoreSlim(0, 1);
        var viewModel = CreateViewModel(onCopy: async (_, cancellationToken) =>
        {
            started.Release();
            await release.WaitAsync(cancellationToken);
            return new BackupResult(new BackupCopy("D:\\copies\\copy", Noon, true), Manifest(), []);
        });
        viewModel.CancelCommand.CanExecuteChanged += (_, _) => cancelNotifications++;

        var run = viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);
        await started.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        Assert.Equal(1, cancelNotifications);
        release.Release();
        await run;

        Assert.False(viewModel.CancelCommand.CanExecute(null));
        Assert.Equal(2, cancelNotifications);
    }

    [Fact]
    public void Every_key_this_view_paints_exists_in_the_dictionary()
    {
        var presentationRoot = RepositoryLayout.PathFromRoot("src", "ApSolutions.LocalMedia.Presentation");
        var view = XDocument.Load(Path.Combine(presentationRoot, "Backup", "BackupView.axaml"));
        var spanish = LoadResourceKeys(Path.Combine(presentationRoot, "Resources", "Strings.es.axaml"));

        // The "no literal words" half of this moved to ViewLiteralTests on 2026-08-22, which states it
        // once over every view instead of twice over two - and states it as what it protects, so the
        // glyphs this tree writes on purpose stop tripping it. What stays here is the half that is
        // this view's own: the keys it paints exist in the dictionary.
        foreach (var key in new[]
        {
            "BackupTitle",
            "BackupDescription",
            "BackupCreateCopyLabel",
            "BackupExportLabel",
            "BackupCancelLabel",
            "BackupStatusIdle",
            "BackupStatusRunning",
            "BackupStatusDone",
            "BackupStatusCancelled",
            "BackupStatusNoSpace",
            "BackupStatusFailed",
            "BackupStageSnapshot",
            "BackupStagePreferences",
            "BackupStagePersonalArtwork",
            "BackupStageManifest",
            "BackupStagePublish",
            "BackupStageArchive",
            "BackupExcludedNotice",
        })
        {
            Assert.Contains(key, spanish);
        }
    }

    /// <summary>
    /// A copy that failed looks like a failure, and one that was cancelled does not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The status block was <c>AccentSubtleBrush</c> whatever it said, so "there was not enough room
    /// on the disk" was painted exactly like "done". §4 asks for the failed state to read as one.
    /// </para>
    /// <para>
    /// <b>Cancelled is deliberately not a failure.</b> Its own string says nothing was left
    /// half-written, and somebody who pressed cancel does not need to be told in red that the thing
    /// they asked to stop stopped. The two failure keys are the ones the model can reach:
    /// <c>BackupStatusFailed</c> and <c>BackupStatusNoSpace</c>.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task A_copy_that_failed_is_painted_as_a_failure_and_a_cancelled_one_is_not()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var failing = CreateViewModel(onCopy: (_, _) =>
            throw new InsufficientBackupSpaceException(requiredBytes: 4_000_000_000, availableBytes: 1_000));
        await failing.CreateCopyAsync(TestContext.Current.CancellationToken);
        Assert.Equal("BackupStatusNoSpace", failing.StatusKey);
        AssertFailureSurface(failing, expected: true);

        var cancelled = CreateViewModel(onCopy: (_, _) => throw new OperationCanceledException());
        await cancelled.CreateCopyAsync(TestContext.Current.CancellationToken);
        Assert.Equal("BackupStatusCancelled", cancelled.StatusKey);
        AssertFailureSurface(cancelled, expected: false);

        var done = CreateViewModel();
        await done.CreateCopyAsync(TestContext.Current.CancellationToken);
        Assert.Equal("BackupStatusDone", done.StatusKey);
        AssertFailureSurface(done, expected: false);
    }

    /// <summary>
    /// The last copy and the last export are shown, having been produced and shown to nobody.
    /// </summary>
    /// <remarks>
    /// Both properties were shaped for a screen — their summaries say "the name of the copy folder,
    /// never the path that leads to it" and "the name of the archive file, never the folder it was
    /// written into" — and <b>no view painted either</b>. Somebody who made a copy had no way to see
    /// that one exists without going to look in the file manager.
    /// </remarks>
    [AvaloniaFact]
    public async Task The_last_copy_and_the_last_export_are_shown_once_they_exist()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));

        var viewModel = CreateViewModel();
        var (emptyWindow, emptyView) = ShowBackup(viewModel);
        Assert.DoesNotContain(
            emptyView.GetVisualDescendants().OfType<Control>(),
            control => control.Name == "BackupLastCopyText" && control.IsEffectivelyVisible);
        emptyWindow.Close();

        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);
        await viewModel.ExportAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.LastCopyName));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.LastArchiveName));

        var (window, view) = ShowBackup(viewModel);
        var painted = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .ToArray();
        Assert.Contains(painted, text => text.Contains(viewModel.LastCopyName!, StringComparison.Ordinal));
        Assert.Contains(painted, text => text.Contains(viewModel.LastArchiveName!, StringComparison.Ordinal));
        window.Close();
    }

    /// <summary>
    /// A failure with no special cause is a failure too.
    /// </summary>
    /// <remarks>
    /// <c>HasFailed</c> names two keys and the suite only ever reached one of them. An exception with
    /// no special handling gives <c>BackupStatusFailed</c>, which is the generic case and the one
    /// somebody is most likely to meet — a disk that went away, a file the antivirus grabbed.
    /// </remarks>
    [Fact]
    public async Task A_failure_with_no_special_cause_is_a_failure_too()
    {
        var viewModel = CreateViewModel(onCopy: (_, _) => throw new InvalidOperationException("no reason"));
        await viewModel.CreateCopyAsync(TestContext.Current.CancellationToken);

        Assert.Equal("BackupStatusFailed", viewModel.StatusKey);
        Assert.True(viewModel.HasFailed);
        Assert.False(viewModel.HasLastCopy);
        Assert.False(viewModel.HasLastArchive);
    }

    private static void AssertFailureSurface(BackupViewModel viewModel, bool expected)
    {
        var (window, view) = ShowBackup(viewModel);
        var surface = Assert.Single(
            view.GetVisualDescendants().OfType<Border>(),
            border => border.Name == "BackupFailureSurface");
        Assert.Equal(expected, surface.IsEffectivelyVisible);
        if (expected)
        {
            var application = Avalonia.Application.Current!;
            Assert.True(application.TryGetResource(
                "DangerSurfaceBrush",
                application.ActualThemeVariant,
                out var danger));
            Assert.Equal(
                Assert.IsAssignableFrom<ISolidColorBrush>(danger).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color);
            Assert.Contains(surface.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "⚠");
        }

        window.Close();
    }

    private static (Window Window, BackupView View) ShowBackup(BackupViewModel viewModel)
    {
        var view = new BackupView { DataContext = viewModel };
        var window = new Window { Width = 900, Height = 800, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, view);
    }

    private static BackupManifest Manifest() => new(
        BackupManifest.CurrentFormatVersion,
        "1.0.0",
        Noon,
        "0",
        null,
        [],
        []);

    private static BackupViewModel CreateViewModel(
        Func<IProgress<BackupProgress>, CancellationToken, Task<BackupResult>>? onCopy = null,
        Func<string, IProgress<BackupProgress>, CancellationToken, Task<ExportResult>>? onExport = null,
        string? destination = "D:\\exports\\library.zip") =>
        new(
            onCopy ?? ((_, _) => Task.FromResult(new BackupResult(
                new BackupCopy("D:\\copies\\copy", Noon, true),
                Manifest(),
                []))),
            onExport ?? ((path, _, _) => Task.FromResult(new ExportResult(path, Manifest(), []))),
            _ => Task.FromResult(destination));

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
