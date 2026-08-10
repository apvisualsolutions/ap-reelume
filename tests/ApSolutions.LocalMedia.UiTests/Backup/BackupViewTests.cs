// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Presentation.Backup;
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
    public void Every_visible_string_on_the_view_comes_from_the_resource_dictionary()
    {
        var presentationRoot = GetPresentationRoot();
        var view = XDocument.Load(Path.Combine(presentationRoot, "Backup", "BackupView.axaml"));
        var spanish = LoadResourceKeys(Path.Combine(presentationRoot, "Resources", "Strings.es.axaml"));

        var literals = view.Descendants()
            .SelectMany(element => element.Attributes())
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "Header")
            .Select(attribute => attribute.Value)
            .Where(value => !value.StartsWith('{'))
            .ToArray();

        Assert.Empty(literals);
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

    private static string GetPresentationRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "ApSolutions.LocalMedia.Presentation");
    }
}
