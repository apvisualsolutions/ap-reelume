// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The lifecycle the release promises: the package arrives, runs, is replaced by a newer one without
/// losing anything, refuses to be replaced by an older one, survives a damaged install, and leaves
/// when asked without taking anyone's data with it.
/// </summary>
/// <remarks>
/// Four of those phases belong to Windows itself and need a clean machine, an administrator, and a
/// signing certificate. None of the three exists here, so <c>eng/verify-package.ps1</c> runs the
/// closest thing that can be run — the unpacked package against a data folder of its own — and
/// declares the rest blocked. The point of this suite is that the declaration cannot be quietly
/// upgraded: a blocked phase must stay blocked while the environment is what it is, and must turn
/// into a real result the moment it is not.
/// </remarks>
public sealed class MsixLifecycleTests
{
    /// <summary>What the unpacked package can be made to do here, and what each phase has to show.</summary>
    private static readonly string[] Substitutes =
        ["unpack", "first-launch", "open-with", "upgrade", "downgrade-refused", "repair", "uninstall"];

    /// <summary>
    /// What only Windows can do to a package, and only on a machine set up to allow it.
    /// </summary>
    /// <remarks>
    /// <c>file-association</c> belongs here rather than among the substitutes: the "Open with…" entry
    /// only exists once Windows has processed the manifest during an install, so unpacking the
    /// package cannot produce it and cannot check it either.
    /// </remarks>
    private static readonly string[] Natives =
        ["windows-install", "windows-upgrade", "windows-repair", "windows-uninstall", "file-association"];

    public static TheoryData<string> SubstitutePhases() => [.. Substitutes];

    public static TheoryData<string> NativePhases() => [.. Natives];

    [Fact]
    public void The_lifecycle_report_covers_every_declared_phase_and_invents_none()
    {
        var reported = Phases().Select(phase => phase.RequiredString("id")).Order(StringComparer.Ordinal).ToArray();
        var declared = Substitutes.Concat(Natives).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(declared, reported);
    }

    [Theory]
    [MemberData(nameof(SubstitutePhases))]
    public void Every_phase_that_can_be_run_here_was_run_and_passed(string id)
    {
        var phase = Phase(id);

        Assert.Equal("substitute", phase.RequiredString("kind"));
        Assert.Equal("Passed", phase.RequiredString("outcome"));
        Assert.False(
            string.IsNullOrWhiteSpace(phase.RequiredString("detail")),
            $"Phase {id} passed without saying what it observed.");
    }

    /// <summary>
    /// How long someone waited before there was a window to look at. Whether one appeared is a yes or
    /// a no, and a yes says nothing about a launch that is drifting towards the deadline: the two are
    /// only distinguishable once the wait is a number that the next run can be compared against.
    /// </summary>
    /// <remarks>
    /// The three are reported together because one number on its own cannot say what it measured.
    /// Every cycle gets a data folder of its own, so all three migrate an empty database through the
    /// same sixteen steps — measured, not assumed — which makes them directly comparable. What is
    /// left over is the difference between them: <c>first-launch</c> is the only one starting
    /// binaries that were unpacked moments earlier and never read, so a first launch that stays
    /// slower than the other two is paying for a cold start rather than for the migration.
    /// </remarks>
    [Theory]
    [InlineData("first-launch")]
    [InlineData("open-with")]
    [InlineData("repair")]
    public void Every_launch_reports_how_long_its_window_took_to_appear(string id)
    {
        var phase = Phase(id);

        Assert.True(
            phase.TryGetProperty("windowMilliseconds", out var measured),
            $"Phase {id} says whether a window appeared but not when, so no run of it can be compared "
                + "with the next one.");
        Assert.True(
            measured.ValueKind == JsonValueKind.Number,
            $"Phase {id} reached the deadline without a window, so there is no wait to report.");

        var waited = measured.GetInt32();
        var deadline = phase.GetProperty("windowTimeoutMilliseconds").GetInt32();

        Assert.InRange(waited, 1, deadline);
    }

    /// <summary>
    /// The gate that keeps the substitution honest. While the machine is what it is, these phases are
    /// blocked and must say why; the moment a clean virtual machine runs this, they stop being
    /// allowed to be blocked at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(NativePhases))]
    public void A_phase_windows_owns_is_blocked_here_and_required_anywhere_it_could_run(string id)
    {
        var phase = Phase(id);
        var environment = Report().GetProperty("environment");
        var runnable = environment.GetProperty("cleanVirtualMachine").GetBoolean()
            && environment.GetProperty("elevated").GetBoolean()
            && environment.GetProperty("signed").GetBoolean();

        Assert.Equal("native", phase.RequiredString("kind"));
        if (runnable)
        {
            Assert.Equal("Passed", phase.RequiredString("outcome"));
            return;
        }

        Assert.Equal("Blocked", phase.RequiredString("outcome"));
        Assert.False(
            string.IsNullOrWhiteSpace(phase.RequiredString("reason")),
            $"Phase {id} is blocked without a reason, which is indistinguishable from being skipped.");
    }

    /// <summary>
    /// Every cycle writes somewhere of its own. Without this the check would have to run against the
    /// one profile folder on the machine, and a lifecycle test that destroys real data is not a test.
    /// </summary>
    [Fact]
    public void Each_cycle_ran_against_a_data_folder_of_its_own()
    {
        var roots = Phases()
            .Where(phase => phase.TryGetProperty("dataRoot", out var value) && value.ValueKind == JsonValueKind.String)
            .Select(phase => phase.RequiredString("dataRoot"))
            .ToArray();

        Assert.NotEmpty(roots);
        foreach (var root in roots)
        {
            Assert.DoesNotContain("APSolutions", root, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// An upgrade that loses the catalogue is not an upgrade. The rows the previous version wrote
    /// have to be readable by the next one, and the preferences have to survive with them.
    /// </summary>
    [Fact]
    public void An_upgrade_keeps_the_catalogue_and_the_preferences_it_inherited()
    {
        var upgrade = Phase("upgrade");
        var before = upgrade.GetProperty("before");
        var after = upgrade.GetProperty("after");

        foreach (var table in new[] { "library_roots", "media_files", "playback_preferences" })
        {
            var kept = before.GetProperty(table).GetInt32();
            Assert.True(kept > 0, $"The upgrade proves nothing: {table} was empty before it ran.");
            Assert.Equal(kept, after.GetProperty(table).GetInt32());
        }

        Assert.True(
            upgrade.GetProperty("schemaVersionAfter").GetInt32()
            >= upgrade.GetProperty("schemaVersionBefore").GetInt32(),
            "The upgrade moved the schema backwards.");
    }

    /// <summary>
    /// The downgrade that would really cost something is not the installer's: it is an older build
    /// opening a database a newer one has already migrated. That has to be refused, not attempted.
    /// </summary>
    [Fact]
    public void An_older_build_refuses_a_database_a_newer_one_has_migrated()
    {
        var phase = Phase("downgrade-refused");

        Assert.True(phase.GetProperty("refused").GetBoolean());
        Assert.False(
            phase.GetProperty("databaseModified").GetBoolean(),
            "The refused downgrade still wrote to the database.");
    }

    /// <summary>
    /// Uninstalling removes the application, never the library. Anything else would make an upgrade
    /// gone wrong indistinguishable from a data loss.
    /// </summary>
    [Fact]
    public void Uninstalling_leaves_the_personal_data_where_it_was()
    {
        var phase = Phase("uninstall");

        Assert.False(phase.GetProperty("applicationPresent").GetBoolean());
        Assert.True(
            phase.GetProperty("dataPresent").GetBoolean(),
            "Removing the application removed the library with it.");
        Assert.True(phase.GetProperty("mediaFilesAfter").GetInt32() > 0);
    }

    /// <summary>
    /// Every container the product declares reaches the "Open with…" menu. Declaring them in the
    /// manifest and never seeing Windows register them would leave `SYS-002` resting on a file
    /// nobody read back.
    /// </summary>
    [Fact]
    public void Every_declared_container_reaches_the_open_with_menu_when_windows_installs_the_package()
    {
        var environment = Report().GetProperty("environment");
        var phase = Phase("file-association");
        if (!environment.GetProperty("cleanVirtualMachine").GetBoolean())
        {
            Assert.Equal("Blocked", phase.RequiredString("outcome"));
            return;
        }

        Assert.Equal("Passed", phase.RequiredString("outcome"));

        // Counted from the domain and not written here. This said "8 of 8" until 2026-08-31 and went
        // red the day a ninth container was approved — the assertion was about "every declared
        // container", and a literal is the one thing that cannot mean that.
        var expected = MediaFileExtensions.All.Count;
        Assert.Contains($"{expected} of {expected}", phase.RequiredString("detail"), StringComparison.Ordinal);

        // Which containers, and not only how many, is asserted where the report carries them:
        // windows-lifecycle.json lists every registry location it found and this one does not. An
        // assertion over a field this report has never had would only have been measuring itself.
    }

    /// <summary>
    /// A loose file plays and is not catalogued. The packaged association is the only new way in, so
    /// it is checked here as well as on the unpackaged application.
    /// </summary>
    [Fact]
    public void Opening_a_loose_file_from_the_association_imports_nothing()
    {
        var phase = Phase("open-with");

        Assert.Equal(0, phase.GetProperty("mediaFilesAfter").GetInt32());
        Assert.Equal(0, phase.GetProperty("libraryRootsAfter").GetInt32());
        Assert.True(phase.GetProperty("accepted").GetBoolean(), "The activation never reached the application.");
    }

    /// <summary>
    /// Repair means the payload comes back from the package, not from wherever it was built.
    /// </summary>
    [Fact]
    public void Repair_restores_the_payload_from_the_package_itself()
    {
        var phase = Phase("repair");

        Assert.True(phase.GetProperty("removedFiles").GetInt32() > 0);
        Assert.Equal(0, phase.GetProperty("missingAfterRepair").GetInt32());
    }

    /// <summary>
    /// The report says which machine produced it, so a green run on a machine that could not have
    /// produced it is visible rather than assumed.
    /// </summary>
    [Fact]
    public void The_report_states_the_conditions_it_was_produced_under()
    {
        var environment = Report().GetProperty("environment");

        Assert.False(string.IsNullOrWhiteSpace(environment.RequiredString("note")));
        _ = environment.GetProperty("cleanVirtualMachine").GetBoolean();
        _ = environment.GetProperty("elevated").GetBoolean();
        _ = environment.GetProperty("signed").GetBoolean();
    }

    private static JsonElement Report() => PackageEvidence.ReadReport("lifecycle.json");

    private static JsonElement.ArrayEnumerator Phases() => Report().GetProperty("phases").EnumerateArray();

    private static JsonElement Phase(string id) =>
        Phases().SingleOrDefault(phase => phase.RequiredString("id") == id) is { ValueKind: JsonValueKind.Object } found
            ? found
            : throw new InvalidOperationException($"The lifecycle report has no phase '{id}'. {PackageEvidence.HowToProduce}");
}
