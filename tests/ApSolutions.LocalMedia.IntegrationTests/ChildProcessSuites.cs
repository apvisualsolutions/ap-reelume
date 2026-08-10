// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests;

/// <summary>
/// The suites that start child test hosts of this very project.
/// <para>
/// Each child is a full test host, so two of them running at once do not merely share the machine —
/// they queue behind each other's start-up and blow through timeouts that are generous for one. Being
/// in the same non-parallel collection is what keeps a twenty-crash endurance run from being timed
/// while another suite is booting a host of its own.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ChildProcessSuites
{
    public const string Name = "child-process";
}
