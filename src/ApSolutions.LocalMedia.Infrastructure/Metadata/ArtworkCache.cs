using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Infrastructure.Privacy;

namespace ApSolutions.LocalMedia.Infrastructure.Metadata;

public enum ArtworkOrigin
{
    Personal,
    RemoteCache,
}

public sealed record ArtworkReference(
    string Path,
    ArtworkOrigin Origin,
    string AlternativeText,
    bool IsExportable);

public sealed class ArtworkCache
{
    /// <summary>
    /// Ten megabytes is the ceiling on one poster (SEC-005): a legitimate TMDB image is a fraction
    /// of it, and anything bigger is a body worth refusing before it is buffered whole.
    /// </summary>
    public const int MaximumArtworkBytes = 10 * 1024 * 1024;

    private readonly string _appDataRoot;
    private readonly string _personalRoot;
    private readonly string _remoteRoot;
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _allowedHosts;

    /// <param name="allowedHosts">
    /// The hosts artwork may be fetched from. Left unset, the allowlist is what the network purpose
    /// registry declares for this component; tests hand in their loopback server explicitly.
    /// </param>
    public ArtworkCache(string appDataRoot, HttpClient httpClient, IReadOnlyList<string>? allowedHosts = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataRoot);
        _appDataRoot = Path.GetFullPath(appDataRoot);
        _personalRoot = Path.Combine(_appDataRoot, "personal-artwork");
        _remoteRoot = Path.Combine(_appDataRoot, "cache", "artwork");
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _allowedHosts = allowedHosts ?? NetworkPurposeRegistry.Require(nameof(ArtworkCache)).Hosts;
    }

    public async Task<ArtworkReference> ImportPersonalAsync(
        TitleId titleId,
        string sourcePath,
        string alternativeText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ValidateAlternativeText(alternativeText);
        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var destinationDirectory = Path.Combine(_personalRoot, titleId.Value.ToString("N"));
        Directory.CreateDirectory(destinationDirectory);
        var extension = Path.GetExtension(sourcePath);
        var destination = Path.Combine(destinationDirectory, $"{Hash(bytes)}{extension}");
        await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
        return new ArtworkReference(destination, ArtworkOrigin.Personal, alternativeText.Trim(), IsExportable: true);
    }

    public async Task<ArtworkReference> CacheRemoteAsync(
        TitleId titleId,
        Uri source,
        string alternativeText,
        ArtworkReference? previous,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateAlternativeText(alternativeText);

        // A host outside the declared purpose is a policy refusal, not a network failure: whatever
        // suggested that address, this component's promise does not cover it (SEC-004).
        if (!_allowedHosts.Any(pattern => NetworkPurpose.Matches(pattern, source.Host)))
        {
            throw new HttpRequestException(
                $"Artwork would be fetched from '{source.Host}', which no declared purpose covers.");
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return previous ?? throw new HttpRequestException(
                    $"Artwork request failed with status {(int)response.StatusCode}.");
            }

            var bytes = await ReadCappedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            var destinationDirectory = Path.Combine(_remoteRoot, titleId.Value.ToString("N"));
            Directory.CreateDirectory(destinationDirectory);
            var extension = MediaExtension(response.Content.Headers.ContentType?.MediaType);
            var destination = Path.Combine(destinationDirectory, $"{Hash(source.AbsoluteUri)}{extension}");
            await File.WriteAllBytesAsync(destination, bytes, cancellationToken).ConfigureAwait(false);
            return new ArtworkReference(destination, ArtworkOrigin.RemoteCache, alternativeText.Trim(), IsExportable: false);
        }
        catch (HttpRequestException) when (previous is not null)
        {
            return previous;
        }
    }

    /// <summary>
    /// Reads a poster with the ceiling in force. Refusing at the limit, mid-stream, is what keeps a
    /// hostile answer from being buffered whole just to be thrown away.
    /// </summary>
    private static async Task<byte[]> ReadCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var body = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            using var buffered = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    return buffered.ToArray();
                }

                if (buffered.Length + read > MaximumArtworkBytes)
                {
                    throw new HttpRequestException(
                        $"Artwork exceeded the {MaximumArtworkBytes} byte ceiling and was refused.");
                }

                buffered.Write(buffer, 0, read);
            }
        }
    }

    public Task<IReadOnlyList<string>> GetExportablePersonalArtworkAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<string> files = Directory.Exists(_personalRoot)
            ? Directory.EnumerateFiles(_personalRoot, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : [];
        return Task.FromResult(files);
    }

    public Task ClearRemoteCacheAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureRemoteRootIsConfined();
        if (Directory.Exists(_remoteRoot))
        {
            Directory.Delete(_remoteRoot, recursive: true);
        }

        Directory.CreateDirectory(_remoteRoot);
        return Task.CompletedTask;
    }

    private void EnsureRemoteRootIsConfined()
    {
        var expected = Path.GetFullPath(Path.Combine(_appDataRoot, "cache", "artwork"));
        if (!string.Equals(expected, _remoteRoot, StringComparison.OrdinalIgnoreCase)
            || !expected.StartsWith(_appDataRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The remote artwork cache is outside the application data root.");
        }
    }

    private static void ValidateAlternativeText(string alternativeText) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(alternativeText);

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string MediaExtension(string? mediaType) => mediaType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg",
    };
}
