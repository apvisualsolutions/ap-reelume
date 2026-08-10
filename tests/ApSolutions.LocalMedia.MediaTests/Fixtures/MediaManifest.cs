// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApSolutions.LocalMedia.MediaTests.Fixtures;

/// <summary>Whether the engine must play a sample or explain, actionably, why it cannot.</summary>
internal enum ExpectedOutcome
{
    Playable,
    ActionableUnsupported,
}

/// <summary>
/// One row of the approved container/codec matrix. Provenance is a generation recipe rather than a
/// redistributed asset, so nothing in the matrix is ever committed.
/// </summary>
internal sealed record MediaSample
{
    public required string Id { get; init; }

    public required string RelativePath { get; init; }

    public required string Container { get; init; }

    public required string VideoCodec { get; init; }

    public string? AudioCodec { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public double? DurationSeconds { get; init; }

    public int VideoTracks { get; init; }

    public int AudioTracks { get; init; }

    public int SubtitleTracks { get; init; }

    public bool Hdr { get; init; }

    /// <summary>Relative path of a text file the recipe muxes in, written before the encoder runs.</summary>
    public string? CompanionTextPath { get; init; }

    public string? CompanionText { get; init; }

    public IReadOnlyList<string> RequiredEncoders { get; init; } = [];

    public required string Recipe { get; init; }

    public string? DerivedFrom { get; init; }

    public double? TruncateToFraction { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter<ExpectedOutcome>))]
    public ExpectedOutcome ExpectedOutcome { get; init; }

    public string? ExpectedFailureCode { get; init; }

    public string? Notes { get; init; }
}

internal sealed record MediaManifestDocument
{
    public int FormatVersion { get; init; }

    public string ProvenanceStatement { get; init; } = string.Empty;

    public IReadOnlyList<MediaSample> Samples { get; init; } = [];
}

/// <summary>
/// Loads the approved matrix and materialises each sample on demand. Every file lands under the
/// ignored artifacts tree; the repository only ever stores the recipe that produces it.
/// </summary>
internal static class MediaManifest
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static MediaManifestDocument Document { get; } = Load();

    public static IReadOnlyList<MediaSample> Samples => Document.Samples;

    public static MediaSample Require(string id) =>
        Samples.SingleOrDefault(sample => sample.Id == id)
            ?? throw new InvalidOperationException($"The manifest has no sample named '{id}'.");

    /// <summary>The encoders this sample needs that the local toolchain cannot provide.</summary>
    public static IReadOnlyList<string> MissingEncoders(MediaSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return [.. sample.RequiredEncoders.Where(encoder => !MediaToolchain.HasEncoder(encoder))];
    }

    /// <summary>Produces the sample, deriving it from its parent when the manifest says so.</summary>
    public static async Task<string> MaterialiseAsync(MediaSample sample, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (sample.DerivedFrom is not { } parentId)
        {
            var recipe = sample.Recipe;
            if (sample.CompanionTextPath is { } companionPath && sample.CompanionText is { } companionText)
            {
                var companion = await MediaToolchain
                    .EnsureTextCompanionAsync(companionPath, companionText, cancellationToken)
                    .ConfigureAwait(false);
                recipe = recipe.Replace("{{companion}}", companion, StringComparison.Ordinal);
            }

            return await MediaToolchain
                .EnsureSampleAsync(sample.RelativePath, recipe, cancellationToken)
                .ConfigureAwait(false);
        }

        var parent = await MaterialiseAsync(Require(parentId), cancellationToken).ConfigureAwait(false);
        var fraction = sample.TruncateToFraction
            ?? throw new InvalidOperationException($"Sample '{sample.Id}' derives without a truncation fraction.");
        return await MediaToolchain
            .EnsureTruncatedSampleAsync(sample.RelativePath, parent, fraction, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Hex SHA-256 of a materialised sample, recorded as provenance in the evidence run.</summary>
    public static async Task<string> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static MediaManifestDocument Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "media-manifest.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The media manifest was not copied to '{path}'.");
        }

        return JsonSerializer.Deserialize<MediaManifestDocument>(File.ReadAllText(path), SerializerOptions)
            ?? throw new InvalidOperationException("The media manifest could not be read.");
    }
}
