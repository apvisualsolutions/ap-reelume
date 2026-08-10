// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Application.Continuity;

/// <summary>
/// A range a person drew by hand. There is deliberately no origin field: this command can only ever
/// create a manual marker.
/// </summary>
public sealed record SaveManualMarkerCommand(
    SeriesId SeriesId,
    MarkerKind Kind,
    TimeSpan Start,
    TimeSpan End,
    TimeSpan? Duration,
    Guid? MarkerId = null);

/// <summary>Why a range was or was not stored.</summary>
public enum SaveMarkerOutcome
{
    Saved,
    InvalidRange,
    Overlaps,
}

/// <summary>The stored marker, or the one that stood in its way.</summary>
public sealed record SaveManualMarkerResult(
    SaveMarkerOutcome Outcome,
    IntroMarker? Marker,
    IntroMarker? Conflict);

/// <summary>
/// Creates or edits one manual range for a series. Nothing here detects anything: the confidence and
/// correction fields exist on the model for a later release and are left unset.
/// </summary>
public sealed class SaveManualMarker
{
    private readonly IIntroMarkerRepository _repository;

    public SaveManualMarker(IIntroMarkerRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<SaveManualMarkerResult> ExecuteAsync(
        SaveManualMarkerCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!MarkerPolicy.IsValidRange(command.Start, command.End, command.Duration))
        {
            return new SaveManualMarkerResult(SaveMarkerOutcome.InvalidRange, null, null);
        }

        var existing = await _repository
            .GetForSeriesAsync(command.SeriesId, cancellationToken)
            .ConfigureAwait(false);
        if (MarkerPolicy.FindConflict(
                existing,
                command.MarkerId,
                command.Kind,
                command.Start,
                command.End) is { } conflict)
        {
            return new SaveManualMarkerResult(SaveMarkerOutcome.Overlaps, null, conflict);
        }

        var marker = new IntroMarker(
            command.MarkerId ?? Guid.NewGuid(),
            command.SeriesId,
            command.Kind,
            command.Start,
            command.End,
            MarkerOrigin.Manual,
            Confidence: null,
            UserCorrected: false);
        await _repository.SaveAsync(marker, cancellationToken).ConfigureAwait(false);
        return new SaveManualMarkerResult(SaveMarkerOutcome.Saved, marker, null);
    }
}
