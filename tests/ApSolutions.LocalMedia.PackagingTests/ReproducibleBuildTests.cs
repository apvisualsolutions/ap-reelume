// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// Two builds of the same commit, from two clean checkouts in two different directories, produce the
/// same payload. Without this the published hash proves only that a file was downloaded intact, not
/// that it is the source it claims to be built from.
/// </summary>
/// <remarks>
/// The container is deliberately not compared. An MSIX is an OPC archive and records the moment it
/// was written, so two identical payloads always yield two different archives; what has to match is
/// everything the archive holds, file by file.
/// </remarks>
public sealed class ReproducibleBuildTests
{
    [Fact]
    public void The_comparison_used_two_clean_checkouts_in_two_different_places()
    {
        var builds = Report().GetProperty("builds").EnumerateArray().ToArray();

        Assert.Equal(2, builds.Length);
        Assert.NotEqual(builds[0].RequiredString("checkout"), builds[1].RequiredString("checkout"));
        Assert.Equal(builds[0].RequiredString("commit"), builds[1].RequiredString("commit"));
        foreach (var build in builds)
        {
            Assert.True(build.GetProperty("clean").GetBoolean(), "A checkout carried uncommitted changes.");
            Assert.True(build.GetProperty("fileCount").GetInt32() > 0);
        }
    }

    [Fact]
    public void Both_builds_produced_the_same_set_of_files()
    {
        var report = Report();
        var onlyInFirst = Names(report, "onlyInFirst");
        var onlyInSecond = Names(report, "onlyInSecond");

        Assert.True(onlyInFirst.Length == 0, $"Only the first build produced: {string.Join(", ", onlyInFirst)}.");
        Assert.True(onlyInSecond.Length == 0, $"Only the second build produced: {string.Join(", ", onlyInSecond)}.");
    }

    [Fact]
    public void Every_file_in_the_payload_hashes_the_same_in_both_builds()
    {
        var report = Report();
        var differing = Names(report, "differingFiles");

        Assert.True(
            differing.Length == 0,
            $"These files differ between two builds of the same commit: {string.Join(", ", differing)}.");
        Assert.True(
            report.GetProperty("identicalFiles").GetInt32() > 0,
            "Nothing was compared, so nothing was reproduced.");
    }

    /// <summary>
    /// An exclusion is how a reproducibility claim quietly stops meaning anything, so any file left
    /// out of the comparison has to be named and defended here rather than in a script.
    /// </summary>
    [Fact]
    public void Nothing_was_excluded_from_the_comparison()
    {
        var excluded = Names(Report(), "excludedFromComparison");

        Assert.True(
            excluded.Length == 0,
            $"These files were left out of the reproducibility comparison: {string.Join(", ", excluded)}.");
    }

    /// <summary>
    /// The comparison has to cover the payload that ships, not a subset of it.
    /// </summary>
    [Fact]
    public void The_comparison_covered_the_whole_shipped_payload()
    {
        var report = Report();
        var contents = PackageEvidence.ReadReport("contents.json");

        Assert.Equal(contents.GetProperty("layoutFileCount").GetInt32(), report.GetProperty("identicalFiles").GetInt32());
    }

    private static JsonElement Report() => PackageEvidence.ReadReport("reproducibility.json");

    private static string[] Names(JsonElement report, string property) =>
        [.. report.GetProperty(property).EnumerateArray().Select(entry => entry.GetString() ?? string.Empty)];
}
