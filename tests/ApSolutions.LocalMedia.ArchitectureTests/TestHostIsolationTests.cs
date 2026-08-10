// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Xml.Linq;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// Guards the approved rule that test assemblies never run at the same time. Concurrent hosts
/// destabilise the native media library, and a host that waits behind fifteen siblings dies on its
/// sixty-second connection timeout instead of reporting a real result.
/// </summary>
public sealed class TestHostIsolationTests
{
    [Fact]
    public void The_run_settings_limit_a_single_invocation_to_one_assembly_at_a_time()
    {
        var runSettingsPath = RepositoryLayout.PathFromRoot("eng/test.runsettings");
        Assert.True(File.Exists(runSettingsPath), "eng/test.runsettings is missing.");

        var maximumCpuCount = XDocument.Load(runSettingsPath)
            .Descendants("MaxCpuCount")
            .Select(element => element.Value)
            .Single();

        Assert.Equal("1", maximumCpuCount);
    }

    [Fact]
    public void The_verification_gate_serialises_the_MSBuild_scheduled_invocations()
    {
        var gatePath = RepositoryLayout.PathFromRoot("eng/verify.ps1");
        Assert.True(File.Exists(gatePath), "eng/verify.ps1 is missing.");

        var commands = File.ReadAllLines(gatePath)
            .Where(line => line.Contains("dotnet test", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.Contains("-m:1", command, StringComparison.Ordinal));
    }
}
