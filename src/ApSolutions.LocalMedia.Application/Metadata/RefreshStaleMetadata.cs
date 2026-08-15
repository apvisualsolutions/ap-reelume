// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>Whether the application may refresh metadata nobody asked it to refresh.</summary>
/// <remarks>
/// The absence of the setting means no, as everywhere else here: an installation that has never been
/// asked the question has not answered it, and a connection is not something to make on the strength
/// of a missing value.
/// </remarks>
public interface IAutoRefreshSettings
{
    bool AutomaticRefreshEnabled { get; }

    void SetAutomaticRefreshEnabled(bool enabled);
}

/// <param name="Attempted">Entries the pass asked the provider about.</param>
/// <param name="Applied">Entries the provider answered for and the catalogue now holds.</param>
public sealed record RefreshStaleMetadataResult(int Attempted, int Applied)
{
    public static readonly RefreshStaleMetadataResult None = new(0, 0);
}

/// <summary>
/// One pass of the automatic refresh: the stalest identified entries, asked about again, capped.
/// </summary>
/// <remarks>
/// <para>
/// Off is the default and off means <b>nothing happens here</b> — the repository is not even read,
/// so a switch that was never turned on cannot cost a request. That is asserted by the network
/// canary rather than by reading this code.
/// </para>
/// <para>
/// It yields to playback the way segment detection does: somebody watching something is the whole
/// point of the application, and a scan already running is the other thing this must not compete
/// with. Both are checked before each entry rather than once, because a pass outlives the moment it
/// started in.
/// </para>
/// </remarks>
public sealed class RefreshStaleMetadata(
    ICatalogMetadataRepository repository,
    RefreshMetadata refresh,
    IAutoRefreshSettings settings,
    IPlaybackActivity playback,
    IScanActivity scans,
    TimeProvider timeProvider)
{
    public async Task<RefreshStaleMetadataResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        if (!settings.AutomaticRefreshEnabled || IsBusy)
        {
            return RefreshStaleMetadataResult.None;
        }

        var stale = await repository.ListStaleAsync(
            MetadataRefreshPolicy.StaleBefore(timeProvider.GetUtcNow()),
            MetadataRefreshPolicy.MaximumPerPass,
            cancellationToken).ConfigureAwait(false);

        var attempted = 0;
        var applied = 0;
        foreach (var entry in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy)
            {
                break;
            }

            attempted++;
            var result = await refresh.ExecuteAsync(
                new RefreshMetadataCommand(entry.TitleId, entry.Revision, RestoreProviderFields: false),
                cancellationToken).ConfigureAwait(false);
            if (result.Outcome == MetadataWriteOutcome.Applied)
            {
                applied++;
            }
        }

        return new RefreshStaleMetadataResult(attempted, applied);
    }

    private bool IsBusy => playback.IsPlaybackActive || scans.IsScanActive;
}
