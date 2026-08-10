// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Application.Updates;

/// <summary>
/// The two steps between an offer and Windows: fetch the package and prove it, then — and only with
/// a confirmation for that exact version — hand it over.
/// </summary>
/// <remarks>
/// They are separate calls because they answer to different things. Downloading answers to whoever
/// pressed a button; installing answers to whoever read what changed. Merging them would produce the
/// one behaviour this task exists to make impossible: a package that installs because it finished
/// downloading.
/// </remarks>
public sealed class ConfirmUpdate
{
    private readonly IUpdateDownloader _downloader;
    private readonly IUpdateLauncher _launcher;
    private readonly InstalledRelease _installed;

    public ConfirmUpdate(IUpdateDownloader downloader, IUpdateLauncher launcher, InstalledRelease installed)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _installed = installed ?? throw new ArgumentNullException(nameof(installed));
    }

    /// <summary>
    /// Fetches and verifies. The policy runs again here rather than being trusted to have run when
    /// the offer was made, because the offer and the download are separated by however long somebody
    /// took to read it.
    /// </summary>
    public async Task<StagedUpdate> StageAsync(
        UpdateRelease release,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        var decision = UpdatePolicy.Decide(release, _installed.Version, _installed.Runtime);
        if (!decision.IsOffered)
        {
            throw new UpdateRefusedException(decision.Rejection, decision.Reason);
        }

        return await _downloader
            .DownloadAsync(release, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The only path that hands anything to Windows.
    /// </summary>
    /// <remarks>
    /// The package is verified a second time immediately before the handover. Everything between the
    /// download and this moment is somebody else's disk, and the whole point of a hash is that it can
    /// be checked again when it matters rather than once when it was convenient.
    /// </remarks>
    public async Task<UpdateInstallation> ExecuteAsync(
        StagedUpdate staged,
        UpdateConsent? consent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(staged);
        if (consent is null)
        {
            return new UpdateInstallation(
                UpdateOutcome.NotConfirmed,
                "Nobody confirmed this update, so nothing was installed.");
        }

        if (!string.Equals(consent.Version, staged.Release.Version, StringComparison.Ordinal))
        {
            return new UpdateInstallation(
                UpdateOutcome.ConsentMismatch,
                $"The confirmation was for '{consent.Version}' and the package is '{staged.Release.Version}'.");
        }

        if (!await StillMatchesAsync(staged, cancellationToken).ConfigureAwait(false))
        {
            Discard(staged.Path);
            return new UpdateInstallation(
                UpdateOutcome.Tampered,
                "The package on disk is no longer the one that was verified, so it was discarded.");
        }

        return await _launcher.LaunchAsync(staged, cancellationToken).ConfigureAwait(false)
            ? new UpdateInstallation(
                UpdateOutcome.HandedToWindows,
                $"Windows was given version {staged.Release.Version} to install.")
            : new UpdateInstallation(
                UpdateOutcome.LaunchRefused,
                "Windows would not open the package. Nothing was replaced, and the verified file is "
                + "still on disk for somebody to install by hand.");
    }

    private static async Task<bool> StillMatchesAsync(StagedUpdate staged, CancellationToken cancellationToken)
    {
        if (!File.Exists(staged.Path))
        {
            return false;
        }

        await using var stream = File.OpenRead(staged.Path);
        if (stream.Length != staged.Release.SizeInBytes)
        {
            return false;
        }

        var actual = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return string.Equals(
            Convert.ToHexString(actual),
            staged.Release.Sha256,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deletes the package that failed the second check. A file that could not be verified has
    /// exactly one use, so leaving it on disk is leaving that use available.
    /// </summary>
    private static void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A package that cannot be deleted is still one that will never be handed over: the
            // verification that just failed is the gate, not the deletion.
        }
    }
}
