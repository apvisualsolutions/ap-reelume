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
/// </remarks>
public sealed class HandoffUpdateTransport : HttpMessageHandler
{
    private readonly string _handoffDirectory;

    public HandoffUpdateTransport(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        _handoffDirectory = handoffDirectory;
    }

    protected override Task<HttpResponseMessage> SendAsync(
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
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        var package = Path.Combine(_handoffDirectory, Path.GetFileName(manifest.PackageFile));
        if (!File.Exists(package))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        var content = new StreamContent(File.OpenRead(package));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
