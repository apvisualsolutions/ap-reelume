// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Metadata;

/// <summary>
/// Hands one web address to whatever browser the person already uses, and never opens it itself.
/// </summary>
/// <remarks>
/// This is what keeps the trailer promise honest: the application makes no connection to YouTube,
/// so <c>NetworkPurposeRegistry</c> gains no purpose and no host. What travels is a request to the
/// operating system; what connects is the browser, under whatever settings, extensions and consent
/// that browser already has.
/// </remarks>
public interface IExternalLinkLauncher
{
    /// <summary>Opens the address externally and reports whether the shell accepted the request.</summary>
    Task<bool> TryLaunchAsync(string link, CancellationToken cancellationToken = default);
}
