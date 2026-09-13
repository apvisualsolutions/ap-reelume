// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

namespace ApSolutions.LocalMedia.Application.Playback;

/// <summary>Releases the active embedded session without disposing the reusable engine.</summary>
public sealed class StopPlayback
{
    private readonly IPlaybackSessionCoordinator _coordinator;

    public StopPlayback(IPlaybackSessionCoordinator coordinator) =>
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

    public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
        _coordinator.StopAsync(cancellationToken);
}
