// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Application.Updates;

/// <summary>
/// Asks the source what it has and lets <see cref="UpdatePolicy"/> decide what that is worth.
/// </summary>
/// <remarks>
/// Three outcomes, and they are kept apart on purpose: nobody asked, the source answered, or the
/// source could not be reached. Software that reports the third as the second tells people they are
/// up to date because it could not find out.
/// </remarks>
public sealed class CheckForUpdates
{
    private readonly IUpdateSource _source;
    private readonly InstalledRelease _installed;
    private readonly IUpdateSettings _settings;

    public CheckForUpdates(IUpdateSource source, InstalledRelease installed, IUpdateSettings settings)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _installed = installed ?? throw new ArgumentNullException(nameof(installed));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<UpdateCheckResult> ExecuteAsync(
        UpdateCheckTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        // A check nobody asked for is a connection nobody asked for. Asking is always allowed; the
        // setting governs only the checks the application would run on its own.
        if (trigger == UpdateCheckTrigger.Automatic && !_settings.AutomaticCheckEnabled)
        {
            return new UpdateCheckResult(UpdateCheckStatus.NotAsked, null, null);
        }

        UpdateRelease? release;
        try
        {
            release = await _source
                .GetLatestAsync(_installed.Runtime, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (UpdateSourceUnavailableException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Unreachable, null, null);
        }

        var decision = UpdatePolicy.Decide(release, _installed.Version, _installed.Runtime);
        return new UpdateCheckResult(
            UpdateCheckStatus.Answered,
            decision,
            decision.IsOffered ? release : null);
    }
}
