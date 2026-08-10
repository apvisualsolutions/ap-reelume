// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Updates;

namespace ApSolutions.LocalMedia.Tests.Updates;

/// <summary>
/// One ephemeral release-signing key per test run, made with the same implementation the product
/// verifies with. The real key's private half lives outside every repository; the tests prove the
/// mechanism, never the secret.
/// </summary>
public static class TestReleaseSigning
{
    private static readonly (string PublicKeyFile, string SecretKeyBase64) Key = Minisign.CreateKeyPair();

    /// <summary>The public half, in the shape the provider embeds.</summary>
    public static string PublicKey => Key.PublicKeyFile;

    /// <summary>
    /// The release-notes sections a signed publication carries: the checksum block and the
    /// detached minisign signature over its canonical bytes, exactly as the release tooling
    /// appends them.
    /// </summary>
    public static string SignedChecksumSections(string sumsCanonical)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sumsCanonical);
        return "## SHA256SUMS\n\n```\n"
            + sumsCanonical.TrimEnd('\n') + "\n```\n\n"
            + "## Firma / Signature\n\n```\n"
            + SignatureFor(sumsCanonical).TrimEnd('\n') + "\n```\n";
    }

    /// <summary>A detached signature over exactly these bytes, UTF-8 encoded.</summary>
    public static string SignatureFor(string content) => Minisign.Sign(
        Convert.FromBase64String(Key.SecretKeyBase64),
        System.Text.Encoding.UTF8.GetBytes(content),
        "SHA256SUMS.txt",
        DateTimeOffset.UnixEpoch);

    /// <summary>A second key nobody publishes with, for signatures that must not verify.</summary>
    public static string ForeignSignatureFor(string content)
    {
        var (_, foreignSecret) = Minisign.CreateKeyPair();
        return Minisign.Sign(
            Convert.FromBase64String(foreignSecret),
            System.Text.Encoding.UTF8.GetBytes(content),
            "SHA256SUMS.txt",
            DateTimeOffset.UnixEpoch);
    }
}
