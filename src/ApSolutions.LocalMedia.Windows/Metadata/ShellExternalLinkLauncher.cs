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
    private readonly Func<ProcessStartInfo, Process?> _start;

    public ShellExternalLinkLauncher()
        : this(Process.Start)
    {
    }

    /// <summary>
    /// Takes the call to the shell as a parameter so the accepting path can be exercised.
    /// </summary>
    /// <remarks>
    /// The coverage gate is what asked for this. Left with <see cref="Process.Start(ProcessStartInfo)"/>
    /// hard-wired, everything past the refusals was unreachable from a test — driving it would open a
    /// real browser on the machine measuring it — and the file sat at 43.75% of lines. Handing the
    /// call in covers the rest and buys a second thing: what reaches the shell can be asserted, which
    /// is the whole point of this class existing.
    /// </remarks>
    public ShellExternalLinkLauncher(Func<ProcessStartInfo, Process?> start) =>
        _start = start ?? throw new ArgumentNullException(nameof(start));

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
            // A null process is success, not failure: the shell hands the address to a browser that
            // is already running — a new tab in a window somebody already had open — and there is no
            // child of this process to report.
            using var process = _start(startInfo);
            return Task.FromResult(true);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No browser is registered for https; the card keeps offering everything else.
            return Task.FromResult(false);
        }
    }
}
