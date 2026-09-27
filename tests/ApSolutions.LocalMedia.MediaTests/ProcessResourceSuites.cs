// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using Xunit;

namespace ApSolutions.LocalMedia.MediaTests;

/// <summary>
/// The suites that measure a resource of the whole process — its handles, its working set — and
/// attribute the growth to the engine.
/// <para>
/// Tests inside an assembly run in parallel, so whatever another class opens while one of these
/// counts is counted too. Measured on 2026-09-27: fifty open-and-close cycles grew the process by
/// 56 to 59 handles run alone, three times out of three, and by <b>206</b> inside the full suite,
/// with the curve jumping from 539 to 1,001 and back to 577 in no relation to the cycles — above the
/// ceiling of 200 with no leak at all. A non-parallel collection runs after the parallel ones and on
/// its own, so what these tests count is the engine and nothing else.
/// </para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProcessResourceSuites
{
    public const string Name = "process-resources";
}
