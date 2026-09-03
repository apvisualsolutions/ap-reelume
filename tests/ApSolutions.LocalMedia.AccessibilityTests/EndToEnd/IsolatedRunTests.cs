// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Metadata;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Metadata;
using ApSolutions.LocalMedia.Windows.Playback;
using ApSolutions.LocalMedia.Windows.Shell;
using ApSolutions.LocalMedia.Windows.Updates;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The isolation rule, as the assembled application obeys it: a run whose data root is not this
/// machine's profile writes nothing and opens nothing outside that root.
/// </summary>
/// <remarks>
/// <para>
/// Three controls were declared uncoverable for one reason — pressing them hands something to
/// Windows itself — and the third time that stopped being a coincidence. Granting startup moved
/// first (<c>IAppDataPaths.StartupRegistrySubKey</c>); this is the second, the provider's trailer
/// link, which asks the shell to open a browser.
/// </para>
/// <para>
/// The rule decides by the <b>resolved root</b> and not by who is asking: somebody who moves their
/// data with <c>AP_LOCALMEDIA_DATA_ROOT</c> is still the person who signs in here, so their browser
/// still opens. A run handed a root that is neither is a run that must leave nothing behind.
/// </para>
/// <para>
/// The isolation is not only what makes the control pressable. Nothing in this repository checked
/// that the address the button would open is the address the stored key composes — the assertion
/// stopped at the view model, because the layer past it went to a real browser.
/// </para>
/// </remarks>
[Collection(AssembledShellSuites.Name)]
public sealed class IsolatedRunTests : IDisposable
{
    private const string WatchAddress = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    private readonly string _dataRoot = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"isolated-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    /// <summary>
    /// A run that keeps its data somewhere of its own writes the address down instead of opening it,
    /// and writes it inside that root.
    /// </summary>
    /// <remarks>
    /// The type is asserted before anything is launched, and that order is the point: were this run
    /// to resolve the launcher that talks to the shell, the call below would open a real browser on
    /// the machine measuring it — which is the very thing the rule exists to prevent.
    /// </remarks>
    [Fact]
    public async Task A_run_that_does_not_own_the_profile_writes_the_address_instead_of_opening_it()
    {
        var isolated = Path.Combine(_dataRoot, "isolated");
        await using var host = ApplicationHost.Create(new AppDataPaths(isolated));

        var launcher = host.Services.GetRequiredService<IExternalLinkLauncher>();
        Assert.IsNotType<ShellExternalLinkLauncher>(launcher);

        Assert.True(await launcher.TryLaunchAsync(WatchAddress, TestContext.Current.CancellationToken));

        // Under its own root, wherever it chose to put it: the rule is about the root, not about a
        // file name this test would then be pinning on the application's behalf.
        var written = Directory.EnumerateFiles(isolated, "*", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains(WatchAddress, StringComparison.Ordinal))
            .ToArray();
        Assert.True(
            written.Length > 0,
            $"An isolated run launched {WatchAddress} and left no record of it anywhere under its "
                + "own root, so nothing here proves it did not simply open a browser.");
    }

    /// <summary>
    /// And the person whose profile this is keeps what the feature is for: their browser opens.
    /// </summary>
    /// <remarks>
    /// The root is named through the environment variable rather than left to the profile folder,
    /// because building an application against the real profile would put a database and a settings
    /// file into whoever ran the suite. Naming the root is exactly what the rule reads, so this is
    /// the owning run — with its data somewhere it can be deleted afterwards.
    /// </remarks>
    [Fact]
    public async Task The_run_that_owns_the_profile_still_hands_addresses_to_the_shell()
    {
        var owned = Path.Combine(_dataRoot, "owned");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, owned);
        try
        {
            await using var host = ApplicationHost.Create(new AppDataPaths());

            // Nothing is launched here on purpose: what this asserts is that the address would go to
            // the shell, and proving it by watching a browser open is not something a suite may do.
            _ = Assert.IsType<ShellExternalLinkLauncher>(
                host.Services.GetRequiredService<IExternalLinkLauncher>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }

    /// <summary>
    /// The same rule at the file dialogs: an isolated run answers its own, and the run that owns the
    /// profile is still the one Windows asks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both halves are here rather than in two tests because merged Cobertura reports keep the better
    /// reading per line and not the union, so a two-way choice split across suites reads as half
    /// covered for good.
    /// </para>
    /// <para>
    /// What is observed is the folder the isolated picker creates in order to answer, which is enough
    /// to tell the two exits apart without the export itself having to succeed. The owning run cannot
    /// be observed any further than this and must not be: proving that its dialog opens would mean
    /// opening one on whoever ran the suite.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_isolated_run_answers_its_own_archive_dialog_and_the_owning_run_does_not()
    {
        var isolated = Path.Combine(_dataRoot, "picker-isolated");
        await using (var host = ApplicationHost.Create(new AppDataPaths(isolated)))
        {
            await host.Services.GetRequiredService<BackupViewModel>()
                .ExportAsync(TestContext.Current.CancellationToken);
        }

        var handoff = new AppDataPaths(isolated).SystemHandoffDirectory;
        Assert.NotNull(handoff);
        Assert.True(
            Directory.Exists(handoff),
            "An isolated run was asked where to export and never answered with a folder of its own, "
                + "so the export went wherever a dialog nobody could see would have said.");

        var owned = Path.Combine(_dataRoot, "picker-owned");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, owned);
        try
        {
            Assert.Null(new AppDataPaths().SystemHandoffDirectory);
            await using var host = ApplicationHost.Create(new AppDataPaths());
            await host.Services.GetRequiredService<BackupViewModel>()
                .ExportAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }

        // Nothing was composed on the owning run's behalf: with no window there is no dialog, and no
        // dialog means cancelled, which is exactly what somebody closing one would have meant. The
        // root may not exist at all, which is the same answer stated more strongly.
        Assert.Empty(Directory.Exists(owned)
            ? Directory.EnumerateFiles(owned, "*.zip", SearchOption.AllDirectories)
            : []);
    }

    /// <summary>
    /// The same rule at what the recovery screen hands to Windows: an isolated run writes down the
    /// folder it would have shown and the shutdown it would have asked for, and the run that owns
    /// the profile is still the one that opens Explorer and ends.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both halves are here rather than in two tests for the reason the archive dialogs are: merged
    /// Cobertura reports keep the better reading per line and not the union, so a two-way choice
    /// split across suites reads as half covered for good.
    /// </para>
    /// <para>
    /// Only the isolated half is driven. Driving the owning one would open an Explorer window on
    /// whoever ran the suite and then end the process running it, which is precisely what the rule
    /// exists to prevent — so what is asserted there is which exit was built, before anything is
    /// asked of it.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_isolated_run_writes_down_what_the_recovery_screen_hands_to_windows()
    {
        var isolated = Path.Combine(_dataRoot, "handoff-isolated");
        var folder = Path.Combine(isolated, "backups");
        await using (var host = ApplicationHost.Create(new AppDataPaths(isolated)))
        {
            var handoff = host.Services.GetRequiredService<ISystemHandoff>();
            Assert.IsNotType<WindowsSystemHandoff>(handoff);

            Assert.True(handoff.TryOpenFolder(folder));
            handoff.RequestExit();

            // The updater's handover is the same exit and is asked through the launcher the
            // application really builds, because what has to be isolated is the installer starting —
            // not a method somebody remembered to call directly.
            var package = Path.Combine(isolated, "apreelume-0.2.0.msix");
            await File.WriteAllTextAsync(
                package,
                "a package",
                TestContext.Current.CancellationToken);
            Assert.True(await host.Services.GetRequiredService<IUpdateLauncher>().LaunchAsync(
                new StagedUpdate(
                    new UpdateRelease("0.2.0", "win-x64", null, null, 9, null, null),
                    package),
                TestContext.Current.CancellationToken));

            // The ninth exit, and it goes through the launcher the application really builds for the
            // same reason: what has to be isolated is a process starting, not a method somebody
            // remembered to call. Its refusals are asserted here too, because a recorder that wrote
            // down handovers the real launcher would have refused would make the probe say nothing
            // about the real one.
            var media = Path.Combine(isolated, "Arrival.2016.mkv");
            await File.WriteAllTextAsync(media, "a film", TestContext.Current.CancellationToken);
            var external = host.Services.GetRequiredService<IExternalPlaybackLauncher>();
            Assert.IsNotType<ShellExternalPlaybackLauncher>(external);
            Assert.True(await external.TryLaunchAsync(media, TestContext.Current.CancellationToken));

            var script = Path.Combine(isolated, "install.ps1");
            await File.WriteAllTextAsync(script, "whoami", TestContext.Current.CancellationToken);
            Assert.False(await external.TryLaunchAsync(script, TestContext.Current.CancellationToken));
            Assert.False(await external.TryLaunchAsync(
                Path.Combine(isolated, "Gone.2014.mkv"),
                TestContext.Current.CancellationToken));
        }

        var record = Path.Combine(
            new AppDataPaths(isolated).SystemHandoffDirectory!,
            RecordingSystemHandoff.FileName);
        Assert.Equal(
            [
                $"{RecordingSystemHandoff.OpenFolderVerb} {folder}",
                RecordingSystemHandoff.ExitVerb,
                $"{RecordingSystemHandoff.OpenPackageVerb} {Path.Combine(isolated, "apreelume-0.2.0.msix")}",
                $"{RecordingSystemHandoff.PlayExternallyVerb} {Path.Combine(isolated, "Arrival.2016.mkv")}",
            ],
            await File.ReadAllLinesAsync(record, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The same rule at the update source: an isolated run is offered whatever its own handover
    /// folder describes, and the run that owns the profile still asks GitHub.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both halves are here for the reason the others are, and this one had a second reason measured
    /// on the day it was written: <c>CompositionDescriptorTests</c> asserted the provider's published
    /// address while composing against a root of its own, and the moment the source began to depend
    /// on the root that assertion **went blind rather than false**. A test about what the application
    /// connects to has to compose as the run that connects, and this is the pair that says so.
    /// </para>
    /// <para>
    /// Only the isolated half is asked anything. Asking the other one would query GitHub from
    /// whichever machine ran the suite, which is the whole thing the rule exists to prevent.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_isolated_run_is_offered_its_own_manifest_and_the_owning_run_asks_github()
    {
        var isolated = Path.Combine(_dataRoot, "source-isolated");
        await using (var host = ApplicationHost.Create(new AppDataPaths(isolated)))
        {
            var source = host.Services.GetRequiredService<IUpdateSource>();
            Assert.IsNotType<GitHubReleaseUpdateProvider>(source);

            // With nothing described there is nothing to install, which is an answer and not a
            // failure — and it is reached without a single connection.
            Assert.Null(await source.GetLatestAsync("win-x64", TestContext.Current.CancellationToken));

            // The download too: what an isolated run replaces is the transport, never the check on
            // what arrived, so it is the same class doing the work on both sides.
            Assert.IsType<HandoffUpdateDownloader>(host.Services.GetRequiredService<IUpdateDownloader>());
        }

        var owned = Path.Combine(_dataRoot, "source-owned");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, owned);
        try
        {
            await using var host = ApplicationHost.Create(new AppDataPaths());

            // Nothing is asked of it on purpose: what this asserts is which source was built, and
            // proving the other half by watching GitHub answer is not something a suite may do.
            _ = Assert.IsType<GitHubReleaseUpdateProvider>(
                host.Services.GetRequiredService<IUpdateSource>());
            _ = Assert.IsType<VerifiedUpdateDownloader>(
                host.Services.GetRequiredService<IUpdateDownloader>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }

    /// <summary>
    /// And the person whose profile this is keeps what the screen is for: Explorer opens on the copy
    /// they can restore from, and leaving ends the application.
    /// </summary>
    [Fact]
    public async Task The_run_that_owns_the_profile_still_hands_the_recovery_screen_to_windows()
    {
        var owned = Path.Combine(_dataRoot, "handoff-owned");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, owned);
        try
        {
            await using var host = ApplicationHost.Create(new AppDataPaths());

            _ = Assert.IsType<WindowsSystemHandoff>(
                host.Services.GetRequiredService<ISystemHandoff>());
            _ = Assert.IsType<ShellExternalPlaybackLauncher>(
                host.Services.GetRequiredService<IExternalPlaybackLauncher>());
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }

    /// <summary>
    /// The same rule at the cover picker (LIB-018): an isolated run answers out of its own handover
    /// folder, and the run that owns the profile is still the one Windows asks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both halves are here rather than in two tests for the reason the archive dialogs are: merged
    /// Cobertura reports keep the better reading per line and not the union, so a two-way choice
    /// split across suites reads as half covered for good.
    /// </para>
    /// <para>
    /// The isolated half is pressed twice on purpose. The first press happens before the handover
    /// folder exists at all, which is the state every isolated run starts in and the one a guard
    /// that only checked for an empty folder would walk straight into; it has to read as a cancelled
    /// dialog, because somebody who chose nothing is not somebody who chose badly. The owning half is
    /// only resolved, never pressed: pressing it would open a file dialog on whoever ran the suite.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task An_isolated_run_answers_its_own_cover_dialog_and_the_owning_run_does_not()
    {
        var isolated = Path.Combine(_dataRoot, "cover-isolated");
        await using (var host = ApplicationHost.Create(new AppDataPaths(isolated)))
        {
            var picker = host.Services.GetRequiredService<ArtworkPickerViewModel>();
            picker.Target = new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000c0"));

            await Press(picker);
            Assert.Null(picker.Status);

            var handoff = new AppDataPaths(isolated).SystemHandoffDirectory;
            Assert.NotNull(handoff);
            Directory.CreateDirectory(handoff);
            await File.WriteAllBytesAsync(
                Path.Combine(handoff, "chosen-cover.png"),
                [1, 2, 3, 4],
                TestContext.Current.CancellationToken);

            await Press(picker);
            Assert.NotNull(picker.Status);
        }

        var owned = Path.Combine(_dataRoot, "cover-owned");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, owned);
        try
        {
            Assert.Null(new AppDataPaths().SystemHandoffDirectory);
            await using var host = ApplicationHost.Create(new AppDataPaths());

            // Which exit was built, asserted before anything is asked of it: with no window there is
            // no dialog, and a picker that could be pressed here would be one opening a window on
            // the machine measuring it.
            var picker = host.Services.GetRequiredService<ArtworkPickerViewModel>();
            picker.Target = new TitleId(Guid.Parse("60000000-0000-0000-0000-0000000000c1"));
            Assert.True(picker.CanChoose);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }

    /// <summary>Presses the picker's button and waits for what it started.</summary>
    /// <remarks>
    /// <c>ICommand.Execute</c> returns void, so the work it starts is not awaitable from here.
    /// <see cref="ArtworkPickerViewModel.IsChoosing"/> is what says whether it is still running, and
    /// polling that is honest about the shape rather than sleeping for a guessed interval.
    /// </remarks>
    private static async Task Press(ArtworkPickerViewModel picker)
    {
        picker.ChooseCoverCommand.Execute(null);

        for (var i = 0; i < 400 && picker.IsChoosing; i++)
        {
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        Assert.False(picker.IsChoosing, "the picker never finished choosing.");
    }
}
