// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.FileSystem;

public sealed class LightweightFingerprintProvider : IFileIdentityProvider
{
    public const int SampleSize = 64 * 1024;

    public const int MaximumSampledBytes = 3 * SampleSize;

    public long LastSampledByteCount { get; private set; }

    public async Task<FileIdentity> GetAsync(
        string path,
        TechnicalMetadata technicalMetadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(technicalMetadata);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: SampleSize,
            FileOptions.Asynchronous | FileOptions.RandomAccess);
        var length = stream.Length;
        var header = FormattableString.Invariant(
            $"sha256:v1\nsize={length}\nduration={technicalMetadata.Duration?.Ticks.ToString(CultureInfo.InvariantCulture) ?? ""}\ncontainer={technicalMetadata.Container}\nvideo={CanonicalCodecs(technicalMetadata.VideoCodecs)}\naudio={CanonicalCodecs(technicalMetadata.AudioCodecs)}\nwidth={technicalMetadata.Width?.ToString(CultureInfo.InvariantCulture) ?? ""}\nheight={technicalMetadata.Height?.ToString(CultureInfo.InvariantCulture) ?? ""}\n");

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(header));
        var offsets = new[]
        {
            0L,
            Math.Max(0, (length - SampleSize) / 2),
            Math.Max(0, length - SampleSize),
        };
        var buffer = new byte[SampleSize];
        long sampledBytes = 0;
        foreach (var offset in offsets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hash.AppendData(BitConverter.GetBytes(offset));
            stream.Position = offset;
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            hash.AppendData(buffer.AsSpan(0, read));
            sampledBytes += read;
        }

        LastSampledByteCount = sampledBytes;
        return new FileIdentity(null, null, $"sha256:v1:{Convert.ToHexString(hash.GetHashAndReset())}");
    }

    private static string CanonicalCodecs(IReadOnlyList<string> codecs) =>
        string.Join(",", codecs.Order(StringComparer.OrdinalIgnoreCase));
}
