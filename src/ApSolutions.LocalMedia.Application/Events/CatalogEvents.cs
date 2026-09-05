// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Application.Events;

/// <summary>
/// How a scan is going, and — since 2026-09-05 — who asked for it.
/// </summary>
/// <remarks>
/// The trigger travels because ADR-0010 makes the answer depend on it: a scan somebody launched by
/// hand may push the content down, and one that starts on its own may not. The enum has existed in
/// this layer since scanning did; what was missing was the trip to the screen, so the surface had no
/// way to tell the two apart and drew neither.
/// </remarks>
public sealed record ScanProgressChanged(
    LibraryRootId RootId,
    int EnumeratedCount,
    int ProbeCount,
    string? CurrentPath,
    bool IsCompleted,
    ScanTrigger Trigger = ScanTrigger.Manual);

/// <summary>
/// A root stopped being readable, or started again. Published where the scan already learns it, so
/// the Library can say it in the one place the affected titles live (ADR-0010).
/// </summary>
public sealed record RootAvailabilityChanged(
    LibraryRootId RootId,
    string Path,
    RootAvailability Availability);

public sealed record CatalogChanged(LibraryRootId RootId, int ChangedCount);

public sealed class InProcessApplicationEventPublisher : IApplicationEventPublisher
{
    public event Action<object>? Published;

    public Task PublishAsync<TEvent>(
        TEvent applicationEvent,
        CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        Published?.Invoke(applicationEvent);
        return Task.CompletedTask;
    }
}
