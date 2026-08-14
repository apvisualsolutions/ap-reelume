// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.Application.Metadata;

namespace ApSolutions.LocalMedia.Windows.Metadata;

/// <summary>
/// Opens a web address with the registered browser. The address is passed as the whole instruction
/// to the shell verb, so no command line is composed and nothing inside it can be interpreted as an
/// argument.
/// </summary>
/// <remarks>
/// <para>
/// The refusals here repeat what <c>TrailerLinkPolicy</c> already guarantees, on purpose and for
/// the same reason the playback launcher re-checks a file extension every caller has already
/// filtered: this is the one place that talks to the shell, so this is where the promise has to
/// hold — not in the good behaviour of everyone who might one day call it.
/// </para>
/// <para>
/// Two checks, and the second is the one a reader does not expect. Requiring <c>https</c> rules out
/// <c>file:</c>, <c>javascript:</c> and every custom scheme some installed application has claimed.
/// Rejecting user information rules out <c>https://www.youtube.com@example.invalid/</c>, which is a
/// valid https address whose host is <c>example.invalid</c> and whose left-hand side is there to be
/// read by a person rather than by the browser. Neither can be produced by the policy today; both
/// are what this layer exists to refuse if something else ever composes an address.
/// </para>
/// </remarks>
public sealed class ShellExternalLinkLauncher : IExternalLinkLauncher
{
    public Task<bool> TryLaunchAsync(string link, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(link, UriKind.Absolute, out var address)
            || !string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || address.UserInfo.Length > 0
            || string.IsNullOrEmpty(address.Host))
        {
            return Task.FromResult(false);
        }

        var startInfo = new ProcessStartInfo(address.AbsoluteUri)
        {
            UseShellExecute = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            return Task.FromResult(process is not null || startInfo.UseShellExecute);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No browser is registered for https; the card keeps offering everything else.
            return Task.FromResult(false);
        }
    }
}
