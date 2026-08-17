// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Updates;

namespace ApSolutions.LocalMedia.Windows.Updates;

/// <summary>
/// The product's own download, over a transport that never leaves the handover folder.
/// </summary>
/// <remarks>
/// <para>
/// What is replaced is the transport and the allowlist, and nothing else:
/// <see cref="VerifiedUpdateDownloader"/> does the work, so the hash, the declared size and the
/// staging under <c>.partial</c> are the ones a person's installation uses. A downloader written for
/// the harness would have proved that the harness's downloader works.
/// </para>
/// <para>
/// The allowlist is the manifest's own host, read per download rather than fixed when this is built.
/// Fixing it at construction would mean the run had to describe its release before anything resolved
/// this service, which is a rule about resolution order that nothing else here has — and a rule
/// nobody can see is a rule that gets broken.
/// </para>
/// </remarks>
public sealed class HandoffUpdateDownloader : IUpdateDownloader
{
    private readonly string _handoffDirectory;
    private readonly string _stagingDirectory;

    public HandoffUpdateDownloader(string handoffDirectory, string stagingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        _handoffDirectory = handoffDirectory;
        _stagingDirectory = stagingDirectory;
    }

    public async Task<StagedUpdate> DownloadAsync(
        UpdateRelease release,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var manifest = HandoffUpdateManifest.Read(_handoffDirectory)
            ?? throw new UpdateSourceUnavailableException(
                "This run was asked to download a release its handover folder no longer describes.");

        using var client = new HttpClient(new HandoffUpdateTransport(_handoffDirectory));
        var downloader = new VerifiedUpdateDownloader(
            client,
            _stagingDirectory,
            [manifest.Address.Host]);
        return await downloader
            .DownloadAsync(release, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
