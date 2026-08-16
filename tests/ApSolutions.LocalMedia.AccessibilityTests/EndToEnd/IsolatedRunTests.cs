// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Metadata;
using ApSolutions.LocalMedia.Windows.Shell;
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
}
