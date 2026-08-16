// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>
/// The one rule that decides whether an address may leave this application at all.
/// </summary>
/// <remarks>
/// <para>
/// Two checks, and the second is the one a reader does not expect. Requiring <c>https</c> rules out
/// <c>file:</c>, <c>javascript:</c> and every custom scheme some installed application has claimed.
/// Rejecting user information rules out <c>https://www.youtube.com@example.invalid/</c>, which is a
/// valid https address whose host is <c>example.invalid</c> and whose left-hand side is there to be
/// read by a person rather than by the browser. Neither can be produced by
/// <see cref="TrailerLinkPolicy"/> today; both are what this exists to refuse if something else ever
/// composes an address.
/// </para>
/// <para>
/// It lives here, in the layer with no I/O, because there is more than one place an address can
/// leave from: the shell opens one for the person whose profile this is, and a run keeping its data
/// somewhere of its own writes the same address down instead. Two exits enforcing a rule each would
/// be two rules the day one of them is edited, and the isolated exit exists precisely so the walk
/// can assert on what the other one would have opened — which is worth nothing if the two disagree
/// about what is allowed through.
/// </para>
/// </remarks>
public static class ExternalLinkPolicy
{
    /// <summary>
    /// The address as this application is willing to hand it over, or nothing at all.
    /// </summary>
    /// <remarks>
    /// The absolute form is what comes back, rather than the caller's text, so what leaves is always
    /// the address that was judged and never a spelling of it that was not.
    /// </remarks>
    public static bool TryAccept(string? link, [NotNullWhen(true)] out Uri? address)
    {
        address = Uri.TryCreate(link, UriKind.Absolute, out var candidate)
            && string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            && candidate.UserInfo.Length == 0
            && candidate.Host.Length > 0
                ? candidate
                : null;
        return address is not null;
    }
}
