// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Net;
using System.Net.Http.Headers;

namespace ApSolutions.LocalMedia.Windows.Updates;

/// <summary>
/// Answers the update download from the handover folder instead of from the network, for a run that
/// does not own this machine's profile.
/// </summary>
/// <remarks>
/// <para>
/// It is a transport and not a downloader, and that distinction is the whole design:
/// <c>VerifiedUpdateDownloader</c> stays exactly as the product built it, so the hash, the declared
/// size and the staging under <c>.partial</c> are the real ones and are exercised for real. What is
/// replaced is only where the bytes come from.
/// </para>
/// <para>
/// The whole file is answered and <c>Range</c> is not implemented. The downloader asks for a range
/// when an earlier attempt left something behind and already treats an answer that is not
/// <see cref="HttpStatusCode.PartialContent"/> as "start from zero" — a server is entitled to ignore
/// a range request, and that path is the product's own. Resuming is measured where it lives, against
/// a loopback server.
/// </para>
/// <para>
/// Only the address the manifest declares is answered, and only with the file it names, resolved
/// inside the handover folder. Nothing here composes a path from the request: a transport that served
/// whatever an address asked for would be a way to read this machine through a manifest.
/// </para>
/// <para>
/// It answers as slowly as the manifest asks it to, which is nearly always at once. A fetch from a
/// folder finishes in milliseconds, so a run that has to press something while one is in flight —
/// the update screen's Cancel exists only while something runs — declares the wait in the data it
/// wrote for itself. The product is untouched by it: the wait takes the caller's own token, so what
/// is stopped is stopped where a slow network would have been stopped.
/// </para>
/// </remarks>
public sealed class HandoffUpdateTransport : HttpMessageHandler
{
    private readonly string _handoffDirectory;

    public HandoffUpdateTransport(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        _handoffDirectory = handoffDirectory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var manifest = HandoffUpdateManifest.Read(_handoffDirectory);
        if (manifest is null || request.RequestUri != manifest.Address)
        {
            // Not what this run was offered. A 404 rather than a throw, because that is what a
            // server answers and the downloader already knows what to do with one.
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var package = Path.Combine(_handoffDirectory, Path.GetFileName(manifest.PackageFile));
        if (!File.Exists(package))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        // Held for as long as the manifest asks, which is for no time at all unless a run has asked
        // otherwise. Only the answer this run was offered is held: a refusal made slowly would
        // measure nothing. There is no branch here because a manifest that declares no wait declares
        // zero, and waiting for zero is the same path as not waiting; what makes this worth having is
        // the token, which is the caller's own, so a fetch stopped here is stopped exactly where a
        // fetch over a slow network would be.
        await Task.Delay(manifest.ServeDelayMilliseconds, cancellationToken).ConfigureAwait(false);

        var content = new StreamContent(File.OpenRead(package));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
