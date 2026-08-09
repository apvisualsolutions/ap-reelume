using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Privacy;

namespace ApSolutions.LocalMedia.Infrastructure.Updates;

/// <summary>
/// Fetches a release package into a staging directory and refuses to produce anything that is not,
/// byte for byte, what the release described.
/// </summary>
/// <remarks>
/// Three properties are worth naming, because each one exists to stop a specific way of getting this
/// wrong.
/// <para>
/// Redirects are followed by hand. An asset URL answers with a redirect to storage, so refusing them
/// outright would download nothing; following them blindly would let the address that is actually
/// fetched be one the policy never saw. Every hop is required to stay on HTTPS.
/// </para>
/// <para>
/// The file is written under a <c>.partial</c> name until it has been verified. A staging directory
/// containing a file with the package's real name means one thing and one thing only: that file has
/// been proved.
/// </para>
/// <para>
/// An interrupted download keeps what arrived; a wrong one deletes it. The distinction matters
/// because resuming from bytes that failed verification would poison every later attempt.
/// </para>
/// </remarks>
public sealed class VerifiedUpdateDownloader : IUpdateDownloader
{
    private const int MaximumRedirects = 5;
    private const int BufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly string _stagingDirectory;
    private readonly IReadOnlyList<string> _allowedHosts;

    /// <param name="allowedHosts">
    /// The hosts a package may be fetched from, every redirect hop included. Left unset, the
    /// allowlist is what the network purpose registry declares for this component — the safe
    /// default; tests hand in their loopback server explicitly.
    /// </param>
    public VerifiedUpdateDownloader(
        HttpClient httpClient,
        string stagingDirectory,
        IReadOnlyList<string>? allowedHosts = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingDirectory);
        _stagingDirectory = Path.GetFullPath(stagingDirectory);
        _allowedHosts = allowedHosts ?? NetworkPurposeRegistry.Require(nameof(VerifiedUpdateDownloader)).Hosts;
    }

    public async Task<StagedUpdate> DownloadAsync(
        UpdateRelease release,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        var address = RequireHttps(release.Url);
        var directory = Path.Combine(_stagingDirectory, Segment(release.Version ?? string.Empty));
        Directory.CreateDirectory(directory);
        var fileName = FileNameFor(address, release.Version ?? string.Empty);
        var finalPath = Path.Combine(directory, fileName);
        var partialPath = finalPath + ".partial";

        await FetchAsync(
            address,
            partialPath,
            Resumable(partialPath, release.SizeInBytes),
            release,
            fileName,
            progress,
            cancellationToken).ConfigureAwait(false);
        await VerifyAsync(partialPath, finalPath, fileName, release, cancellationToken).ConfigureAwait(false);
        return new StagedUpdate(release, finalPath);
    }

    private async Task FetchAsync(
        Uri address,
        string partialPath,
        long alreadyOnDisk,
        UpdateRelease release,
        string fileName,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var current = address;
        for (var hop = 0; hop <= MaximumRedirects; hop++)
        {
            RequireAllowedHost(current);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            if (alreadyOnDisk > 0)
            {
                request.Headers.Range = new RangeHeaderValue(alreadyOnDisk, null);
            }

            HttpResponseMessage response;
            try
            {
                response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                throw new UpdateSourceUnavailableException(
                    "The release package could not be requested.",
                    exception);
            }
            catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new UpdateSourceUnavailableException("The download timed out.", exception);
            }

            using (response)
            {
                if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
                {
                    current = RequireHttps(new Uri(current, location).ToString());
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new UpdateSourceUnavailableException(
                        $"The release package was answered with {(int)response.StatusCode}.");
                }

                // A server is entitled to ignore a range request and send the whole file. Appending
                // that reply to what is already on disk would double the package and fail the hash
                // for a reason that has nothing to do with the package.
                var resumeFrom = response.StatusCode == HttpStatusCode.PartialContent ? alreadyOnDisk : 0;
                await WriteAsync(
                    response,
                    partialPath,
                    resumeFrom,
                    release,
                    fileName,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                return;
            }
        }

        throw new UpdateSourceUnavailableException(
            $"The release package was redirected more than {MaximumRedirects} times without arriving.");
    }

    private static async Task WriteAsync(
        HttpResponseMessage response,
        string partialPath,
        long resumeFrom,
        UpdateRelease release,
        string fileName,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var received = resumeFrom;
        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            var file = new FileStream(
                partialPath,
                resumeFrom > 0 ? FileMode.Open : FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            await using (file.ConfigureAwait(false))
            {
                if (resumeFrom > 0)
                {
                    file.Seek(resumeFrom, SeekOrigin.Begin);
                }

                var buffer = new byte[BufferSize];
                while (true)
                {
                    int read;
                    try
                    {
                        read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is IOException or HttpRequestException)
                    {
                        // What arrived stays on disk under the partial name: the next attempt asks
                        // for the rest rather than for the whole package again.
                        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
                        throw new UpdateInterruptedException(fileName, received, release.SizeInBytes);
                    }

                    if (read == 0)
                    {
                        break;
                    }

                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress?.Report(new UpdateDownloadProgress(received, release.SizeInBytes));

                    // A server that keeps sending past the declared size is cut off at the excess:
                    // whatever it has in store, it is not the package the release described, and
                    // there is no reason to keep writing it to find that out (SEC-005).
                    if (received > release.SizeInBytes)
                    {
                        break;
                    }
                }

                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (received > release.SizeInBytes)
        {
            Discard(partialPath);
            throw new UpdateVerificationException(
                fileName,
                (release.Sha256 ?? string.Empty).ToLowerInvariant(),
                actualSha256: "(nunca calculado: llegaron más bytes de los declarados / never computed: more bytes than declared)",
                release.SizeInBytes,
                received);
        }
    }

    /// <summary>
    /// The moment the download becomes a package. Until the name changes, nothing in the staging
    /// directory can be mistaken for something that was proved.
    /// </summary>
    private static async Task VerifyAsync(
        string partialPath,
        string finalPath,
        string fileName,
        UpdateRelease release,
        CancellationToken cancellationToken)
    {
        long size;
        string actual;
        var stream = File.OpenRead(partialPath);
        await using (stream.ConfigureAwait(false))
        {
            size = stream.Length;
            actual = Convert
                .ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false))
                .ToLowerInvariant();
        }

        var expected = (release.Sha256 ?? string.Empty).ToLowerInvariant();
        if (size != release.SizeInBytes || !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            Discard(partialPath);
            throw new UpdateVerificationException(fileName, expected, actual, release.SizeInBytes, size);
        }

        File.Move(partialPath, finalPath, overwrite: true);
    }

    /// <summary>
    /// How much of an earlier attempt can be continued. A leftover as long as the package, or longer,
    /// is not a resumable download: it is a different file wearing the same name.
    /// </summary>
    private static long Resumable(string partialPath, long declaredSize)
    {
        if (!File.Exists(partialPath))
        {
            return 0;
        }

        var length = new FileInfo(partialPath).Length;
        if (length > 0 && length < declaredSize)
        {
            return length;
        }

        Discard(partialPath);
        return 0;
    }

    /// <summary>
    /// Every hop has to stay on a host the allowlist covers (SEC-004). The redirect target is a
    /// remote server's suggestion, and the address that is actually fetched is a promise the
    /// privacy statement makes — so a hop outside the list is refused, never followed.
    /// </summary>
    private void RequireAllowedHost(Uri address)
    {
        if (!_allowedHosts.Any(pattern => NetworkPurpose.Matches(pattern, address.Host)))
        {
            throw new UpdateRefusedException(
                UpdateRejection.UndeclaredHost,
                $"The package would be fetched from '{address.Host}', which no declared purpose covers.");
        }
    }

    private static Uri RequireHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && parsed.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            ? parsed
            : throw new UpdateRefusedException(
                UpdateRejection.InsecureDownload,
                $"The package would be fetched over something other than HTTPS: '{url}'.");

    private static bool IsRedirect(HttpStatusCode status) => (int)status is >= 300 and <= 399;

    /// <summary>
    /// The package keeps the name the release gave it, with anything a path could use stripped out.
    /// The name comes from a remote server, so it is treated as a suggestion rather than a location.
    /// </summary>
    private static string FileNameFor(Uri address, string version)
    {
        var candidate = Path.GetFileName(address.LocalPath);
        return string.IsNullOrWhiteSpace(candidate)
            ? $"apreelume-{Segment(version)}.msix"
            : Segment(candidate);
    }

    private static string Segment(string value)
    {
        var cleaned = new string([.. value.Where(character =>
            !Path.GetInvalidFileNameChars().Contains(character))]);
        return cleaned.Trim('.', ' ') is { Length: > 0 } safe ? safe : "update";
    }

    private static void Discard(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file staying behind is survivable: it is under the partial name, so nothing will
            // ever offer it, and the next attempt overwrites it.
        }
    }
}
