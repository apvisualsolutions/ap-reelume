using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Infrastructure.Updates;

/// <summary>
/// Reads the latest published release from GitHub and translates it into the description
/// <see cref="UpdatePolicy"/> decides about.
/// </summary>
/// <remarks>
/// It translates and never repairs. A release with no hash in its notes becomes a description with
/// no hash, and the policy refuses it by name; inventing a plausible value here would move the
/// decision from a place that has rules to a place that has guesses.
/// <para>
/// Everything comes from the one answer the API already gives: the tag is the version, the asset
/// carries the size and the address, and the notes carry the bilingual summary and the same
/// <c>SHA256SUMS</c> lines that accompany the published files. A second request for a checksum file
/// would be a second thing that can be missing, stale, or substituted.
/// </para>
/// <para>
/// Drafts and pre-releases are not offered. This application ships stable versions, and a tag nobody
/// promised anything about is not one to replace somebody's working installation with.
/// </para>
/// </remarks>
public sealed class GitHubReleaseUpdateProvider : IUpdateSource
{
    private static readonly Regex ChecksumLine = new(
        @"(?m)^\s*(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>\S+)\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;
    private readonly MinisignPublicKey? _signingKey;

    public GitHubReleaseUpdateProvider(
        HttpClient httpClient,
        string owner,
        string repository,
        string? signingPublicKey = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        _owner = owner;
        _repository = repository;

        // The key is parsed here so a binary shipped with a corrupted key fails at composition,
        // loudly, rather than refusing every release forever with a reason that blames the source.
        _signingKey = signingPublicKey is null ? null : Minisign.ParsePublicKey(signingPublicKey);
    }

    /// <summary>Where this provider actually asks. Readable so the composed address can be asserted.</summary>
    public string RepositoryOwner => _owner;

    /// <summary>The repository half of the address, exposed for the same assertion.</summary>
    public string RepositoryName => _repository;

    public async Task<UpdateRelease?> GetLatestAsync(
        string runtime,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtime);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"repos/{Uri.EscapeDataString(_owner)}/{Uri.EscapeDataString(_repository)}/releases/latest");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AP-Reelume-Updater", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new UpdateSourceUnavailableException("The update source could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UpdateSourceUnavailableException("The update source did not answer in time.", exception);
        }

        using (response)
        {
            // No release is an answer. Anything else that is not a success is a question that was
            // never answered, and the two must not arrive at the caller looking alike.
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateSourceUnavailableException(
                    $"The update source answered with {(int)response.StatusCode}.");
            }

            var payload = await ReadCappedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            try
            {
                return Describe(payload, runtime, _signingKey);
            }
            catch (JsonException exception)
            {
                // A captive portal answers 200 with a login page; a proxy answers with anything.
                // A body that is not a release is a source that was never really reached.
                throw new UpdateSourceUnavailableException(
                    "The update source did not answer with a readable release.",
                    exception);
            }
        }
    }

    /// <summary>
    /// One megabyte is the ceiling on "what is the latest release?" (SEC-005). A source that
    /// answers more is not answering the question, whatever its bytes would have parsed as, and it
    /// is cut off at the ceiling rather than buffered whole to find out.
    /// </summary>
    private const int MaximumMetadataBytes = 1_048_576;

    private static async Task<string> ReadCappedAsync(
        HttpContent content,
        CancellationToken cancellationToken)
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
                    return System.Text.Encoding.UTF8.GetString(buffered.GetBuffer(), 0, (int)buffered.Length);
                }

                if (buffered.Length + read > MaximumMetadataBytes)
                {
                    throw new UpdateSourceUnavailableException(
                        "The update source answered with more metadata than a release description can be.");
                }

                buffered.Write(buffer, 0, read);
            }
        }
    }

    private static UpdateRelease? Describe(string payload, string runtime, MinisignPublicKey? signingKey)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (Flag(root, "draft") || Flag(root, "prerelease"))
        {
            return null;
        }

        if (Text(root, "tag_name") is not { Length: > 0 } tag)
        {
            return null;
        }

        var suffix = $"_{Architecture(runtime)}.msix";
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (Text(asset, "name") is not { Length: > 0 } name
                || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var notes = Text(root, "body") ?? string.Empty;

            // When the checksum block verifies against the release key, the hash is read from the
            // verified bytes and nowhere else — a second, unsigned line for the same file name
            // elsewhere in the notes must not be able to shadow the signed one. When nothing
            // verifies, the claim is still surfaced and the policy refuses it by its right name.
            var verifiedSums = VerifiedChecksumBlock(notes, signingKey);
            var checksum = ChecksumFor(verifiedSums ?? notes, name);
            return new UpdateRelease(
                tag.TrimStart('v', 'V'),
                runtime,
                Text(asset, "browser_download_url"),
                checksum,
                Size(asset),
                Section(notes, "Español"),
                Section(notes, "English"))
            {
                Sha256Signed = verifiedSums is not null && checksum is not null,
            };
        }

        return null;
    }

    /// <summary>
    /// `win-x64` is built for `x64`. The architecture is the last segment because that is how the
    /// runtime identifier is written, not because of a list this file would have to keep current.
    /// </summary>
    private static string Architecture(string runtime)
    {
        var separator = runtime.LastIndexOf('-');
        return separator >= 0 && separator < runtime.Length - 1 ? runtime[(separator + 1)..] : runtime;
    }

    /// <summary>
    /// The canonical content of the checksum block, when the notes carry one and a minisign
    /// signature over it that verifies against the release key. Null in every other case: no key
    /// embedded, no block, no signature, or a signature that does not hold.
    /// </summary>
    /// <remarks>
    /// The canonical bytes are the checksum lines, trimmed, joined by a line feed with one at the
    /// end — the exact bytes the packaging writes into <c>SHA256SUMS.txt</c> and the signing step
    /// signs, so verification here is verification of the published file, not of a lookalike.
    /// </remarks>
    private static string? VerifiedChecksumBlock(string notes, MinisignPublicKey? signingKey)
    {
        if (signingKey is null)
        {
            return null;
        }

        string? sums = null;
        MinisignSignature? signature = null;
        foreach (var block in FencedBlocks(notes))
        {
            var lines = block
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length > 0 && lines.All(line => ChecksumLine.IsMatch(line)))
            {
                sums ??= string.Join('\n', lines) + "\n";
            }
            else if (block.TrimStart().StartsWith("untrusted comment:", StringComparison.Ordinal)
                && Minisign.TryParseSignature(block, out var parsed))
            {
                signature ??= parsed;
            }
        }

        return sums is not null
            && signature is not null
            && Minisign.Verify(signingKey, System.Text.Encoding.UTF8.GetBytes(sums), signature)
                ? sums
                : null;
    }

    /// <summary>The inside of every ``` fence in the notes, in order.</summary>
    private static IEnumerable<string> FencedBlocks(string notes)
    {
        var collected = new List<string>();
        var collecting = false;
        foreach (var line in notes.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (collecting)
                {
                    yield return string.Join('\n', collected);
                    collected.Clear();
                }

                collecting = !collecting;
                continue;
            }

            if (collecting)
            {
                collected.Add(line);
            }
        }
    }

    /// <summary>The published checksum of one file, read from the notes that accompany it.</summary>
    private static string? ChecksumFor(string notes, string assetName)
    {
        foreach (Match match in ChecksumLine.Matches(notes))
        {
            if (string.Equals(match.Groups["name"].Value, assetName, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["hash"].Value.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// One heading's worth of the notes. The release notes are the same bilingual document the
    /// repository writes everywhere else, so the summary is read from it rather than duplicated into
    /// a field somebody would have to remember to fill.
    /// </summary>
    /// <remarks>
    /// Only headings of the same level end a section. A changelog entry is written with `###`
    /// subheadings under each version, and treating those as the next section would cut the summary
    /// at the first one — or, when a version begins with a subheading, leave it empty and have the
    /// release refused for carrying no summary at all.
    /// </remarks>
    private static string? Section(string notes, string heading)
    {
        var collected = new List<string>();
        var collecting = false;
        foreach (var line in notes.ReplaceLineEndings("\n").Split('\n'))
        {
            if (IsSectionHeading(line))
            {
                if (collecting)
                {
                    break;
                }

                collecting = string.Equals(
                    line.TrimStart('#').Trim(),
                    heading,
                    StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (collecting)
            {
                collected.Add(line);
            }
        }

        return string.Join('\n', collected).Trim() is { Length: > 0 } text ? text : null;
    }

    private static bool IsSectionHeading(string line) =>
        line.StartsWith("##", StringComparison.Ordinal) && (line.Length == 2 || line[2] != '#');

    private static bool Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long Size(JsonElement asset) =>
        asset.TryGetProperty("size", out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt64(out var size)
            ? size
            : 0;
}
