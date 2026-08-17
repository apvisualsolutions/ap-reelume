// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Windows.Updates;

/// <summary>
/// Where a release comes from for a run that does not own this machine's profile: its own handover
/// folder, and never the network.
/// </summary>
/// <remarks>
/// <para>
/// It sits beside <c>GitHubReleaseUpdateProvider</c> because the two are the same exit, decided in
/// the composition by the resolved data root the way every other handover is. A harness cannot and
/// must not reach the network — an update check would ask GitHub about releases from whichever
/// machine happened to run the suite.
/// </para>
/// <para>
/// The runtime is answered rather than filtered afterwards, exactly as the contract asks: a source
/// with nothing for this architecture answers with nothing, which is a different thing from
/// answering with a package for another one. The policy still refuses the mismatch, so a manifest
/// that names the wrong runtime stays a refusal somebody can read.
/// </para>
/// </remarks>
public sealed class HandoffUpdateSource : IUpdateSource
{
    private readonly string _handoffDirectory;

    public HandoffUpdateSource(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        _handoffDirectory = handoffDirectory;
    }

    public Task<UpdateRelease?> GetLatestAsync(
        string runtime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtime);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(HandoffUpdateManifest.Read(_handoffDirectory)?.ToRelease());
    }
}
