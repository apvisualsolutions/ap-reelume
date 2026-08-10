// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// What Windows does with the package the updater hands it, measured on both kinds of machine.
/// </summary>
/// <remarks>
/// The application does not install anything: it opens the verified file the way a person would and
/// reports whether Windows took it. Whether that report is true depends on something no test on this
/// side of the boundary can decide — what <c>ShellExecute</c> returns — and it turns out to answer
/// differently on the two populations of machines that matter.
/// <para>
/// On a machine with nothing registered for `.msix` it returns nothing and throws nothing, so the
/// refusal comes from the null and not from the catch. On a machine with the App Installer it
/// returns a process and the installer appears. Both were run by hand: the first inside Windows
/// Sandbox, the second on hardware that already had an App Installer, installing nothing.
/// </para>
/// <para>
/// This suite is what stops that measurement from quietly ceasing to describe the product. It
/// expires with the manifest, for the same reason the lifecycle report does: the manifest is what
/// Windows reads when it installs.
/// </para>
/// </remarks>
public sealed class UpdateHandoverTests
{
    /// <summary>
    /// The report describes this package or it describes nothing. A stale one would be a measurement
    /// of a different artifact wearing this one's name.
    /// </summary>
    [Fact]
    public void The_archived_handover_describes_the_package_this_repository_builds()
    {
        var report = Report();

        Assert.Equal(PackageEvidence.DeclaredVersion(), report.RequiredString("version"));
        Assert.Equal(ManifestSha256(), report.RequiredString("manifestSha256"));
        Assert.False(string.IsNullOrWhiteSpace(report.RequiredString("note")));
    }

    /// <summary>
    /// A machine that cannot open the package installs nothing, and — the part that matters — loses
    /// nothing. The update fails; the library is not a participant.
    /// </summary>
    [Fact]
    public void A_machine_with_no_handler_installs_nothing_and_loses_nothing()
    {
        var handover = Phase(Report().GetProperty("withoutHandler"), "updater-handover");

        Assert.False(handover.GetProperty("startedAHandler").GetBoolean());
        Assert.False(handover.GetProperty("installedWithoutHelp").GetBoolean());
        Assert.True(
            handover.GetProperty("databaseSurvived").GetBoolean(),
            "A failed handover took the database with it.");
        Assert.Equal(
            handover.GetProperty("databaseBytesBefore").GetInt64(),
            handover.GetProperty("databaseBytesAfter").GetInt64());
    }

    /// <summary>
    /// Whatever came out of the call, the shipped launcher survives it. An exception it does not
    /// catch would escape into the update surface and take it down; no exception at all is the case
    /// the composition root handles by testing for null, which is the path actually measured.
    /// </summary>
    [Fact]
    public void Whatever_the_call_produced_is_something_the_launcher_handles()
    {
        var handover = Phase(Report().GetProperty("withoutHandler"), "updater-handover");

        Assert.True(
            handover.GetProperty("caughtByLauncher").GetBoolean(),
            $"The handover threw {handover.RequiredString("exceptionType")}, which the launcher lets escape.");
    }

    /// <summary>
    /// Before any of that means anything, Windows has to have installed the package and the
    /// application has to have written where it says it writes. Otherwise the handover was measured
    /// against an installation that was never there.
    /// </summary>
    [Fact]
    public void Windows_installed_the_package_and_the_application_wrote_where_it_documents()
    {
        var machine = Report().GetProperty("withoutHandler");

        Assert.Equal("Passed", Phase(machine, "windows-install").RequiredString("outcome"));
        var launch = Phase(machine, "windows-launch");
        Assert.Equal("Passed", launch.RequiredString("outcome"));
        Assert.True(launch.GetProperty("databaseBytes").GetInt64() > 0);
    }

    /// <summary>
    /// The inverse, and the reason the other half had to be measured at all: `ShellExecute` may hand
    /// a file to a process that already exists and return nothing, which would have the application
    /// report a refusal while the installer was on screen. On a machine with an App Installer it
    /// returns a process, so the refusal cannot be wrong in that direction.
    /// </summary>
    [Fact]
    public void A_machine_with_a_handler_is_told_the_truth_about_it()
    {
        var withHandler = Report().GetProperty("withHandler");

        Assert.True(withHandler.GetProperty("returnedProcess").GetBoolean());
        Assert.True(withHandler.GetProperty("somethingOpened").GetBoolean());
        Assert.Equal("HandedToWindows", withHandler.RequiredString("launcherWouldSay"));
        Assert.False(
            withHandler.GetProperty("misreportsARefusal").GetBoolean(),
            "The application would report a refusal while the installer was opening.");
        Assert.False(string.IsNullOrWhiteSpace(withHandler.RequiredString("appInstallerVersion")));
    }

    private static string ManifestSha256()
    {
        var path = Path.Combine(PackageEvidence.PackageProjectRoot(), "Package.appxmanifest");
        return Convert
            .ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
    }

    private static JsonElement Report()
    {
        var path = Path.Combine(
            PackageEvidence.RepositoryRoot(),
            "docs",
            "evidence",
            "stable",
            "updater-handover.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "There is no archived handover measurement, so nothing is known about what Windows "
                + "does with the package the updater hands it. eng/README-sandbox.md records how to run it.",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    private static JsonElement Phase(JsonElement machine, string id) =>
        machine.GetProperty("phases").EnumerateArray()
            .SingleOrDefault(phase => phase.RequiredString("id") == id) is { ValueKind: JsonValueKind.Object } found
            ? found
            : throw new InvalidOperationException($"The handover report has no phase '{id}'.");
}
