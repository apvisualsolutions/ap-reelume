using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace ApSolutions.LocalMedia.Infrastructure.Updates;

/// <summary>A minisign public key: eight bytes naming it and thirty-two of Ed25519.</summary>
public sealed record MinisignPublicKey(byte[] KeyId, byte[] Key);

/// <summary>
/// One detached minisign signature: the per-file signature, and the global one that seals the
/// trusted comment to it so the comment cannot be swapped under a valid file signature.
/// </summary>
public sealed record MinisignSignature(
    bool IsPrehashed,
    byte[] KeyId,
    byte[] FileSignature,
    string TrustedComment,
    byte[] GlobalSignature);

/// <summary>
/// The minisign format, read and written directly. Verification is what the product runs;
/// signing and key generation exist for the release tooling and the tests, so both sides of the
/// format live next to each other and cannot drift apart.
/// </summary>
/// <remarks>
/// minisign signs with Ed25519, by default over the Blake2b-512 of the file ("ED") and in its
/// legacy mode over the raw bytes ("Ed"). Both are accepted here, because both are valid
/// signatures the reference tool produces and verifies. The private key never appears in this
/// repository in any form: signing reads it from wherever the caller keeps it.
/// </remarks>
public static class Minisign
{
    private const int KeyIdLength = 8;
    private const int PublicKeyLength = 32;
    private const int SeedLength = 32;
    private const int SignatureLength = 64;

    /// <summary>
    /// Reads a public key from the base64 the format defines, alone or inside the two-line file
    /// minisign writes (untrusted comment, then the key).
    /// </summary>
    public static MinisignPublicKey ParsePublicKey(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var encoded = text
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => !line.StartsWith("untrusted comment:", StringComparison.Ordinal))
            ?? throw new FormatException("The public key text carries no key line.");
        var bytes = Convert.FromBase64String(encoded);
        if (bytes.Length != 2 + KeyIdLength + PublicKeyLength || bytes[0] != (byte)'E' || bytes[1] != (byte)'d')
        {
            throw new FormatException("The public key is not an Ed25519 minisign key.");
        }

        return new MinisignPublicKey(bytes[2..(2 + KeyIdLength)], bytes[(2 + KeyIdLength)..]);
    }

    /// <summary>Reads a detached signature in the four-line shape the format defines.</summary>
    public static bool TryParseSignature(string text, out MinisignSignature? signature)
    {
        signature = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lines = text
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 4
            || !lines[0].StartsWith("untrusted comment:", StringComparison.Ordinal)
            || !lines[2].StartsWith("trusted comment:", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] fileSignature;
        byte[] globalSignature;
        try
        {
            fileSignature = Convert.FromBase64String(lines[1]);
            globalSignature = Convert.FromBase64String(lines[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (fileSignature.Length != 2 + KeyIdLength + SignatureLength
            || globalSignature.Length != SignatureLength
            || fileSignature[0] != (byte)'E')
        {
            return false;
        }

        var isPrehashed = fileSignature[1] == (byte)'D';
        if (!isPrehashed && fileSignature[1] != (byte)'d')
        {
            return false;
        }

        signature = new MinisignSignature(
            isPrehashed,
            fileSignature[2..(2 + KeyIdLength)],
            fileSignature[(2 + KeyIdLength)..],
            lines[2]["trusted comment:".Length..].Trim(),
            globalSignature);
        return true;
    }

    /// <summary>
    /// Whether this signature was made over this content by the holder of this key. Both halves
    /// must hold: the file signature over the content, and the global one over the file signature
    /// and the trusted comment together.
    /// </summary>
    public static bool Verify(MinisignPublicKey publicKey, ReadOnlySpan<byte> content, MinisignSignature signature)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentNullException.ThrowIfNull(signature);
        if (!CryptographicOperations.FixedTimeEquals(publicKey.KeyId, signature.KeyId))
        {
            return false;
        }

        var message = signature.IsPrehashed ? Blake2b512(content) : content.ToArray();
        var key = new Ed25519PublicKeyParameters(publicKey.Key);
        if (!VerifyEd25519(key, message, signature.FileSignature))
        {
            return false;
        }

        var trusted = System.Text.Encoding.UTF8.GetBytes(signature.TrustedComment);
        var globalMessage = new byte[signature.FileSignature.Length + trusted.Length];
        signature.FileSignature.CopyTo(globalMessage, 0);
        trusted.CopyTo(globalMessage, signature.FileSignature.Length);
        return VerifyEd25519(key, globalMessage, signature.GlobalSignature);
    }

    /// <summary>
    /// Signs content the way minisign does by default: Ed25519 over its Blake2b-512, sealed with
    /// the global signature. Used by the release tooling and the tests, never by the product.
    /// </summary>
    public static string Sign(byte[] secretKey, ReadOnlySpan<byte> content, string fileName, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(secretKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (secretKey.Length != 2 + KeyIdLength + SeedLength || secretKey[0] != (byte)'E' || secretKey[1] != (byte)'d')
        {
            throw new FormatException("The secret key is not in the release-signing format (Ed + key id + seed).");
        }

        var keyId = secretKey[2..(2 + KeyIdLength)];
        var privateKey = new Ed25519PrivateKeyParameters(secretKey[(2 + KeyIdLength)..]);
        var fileSignature = new byte[2 + KeyIdLength + SignatureLength];
        fileSignature[0] = (byte)'E';
        fileSignature[1] = (byte)'D';
        keyId.CopyTo(fileSignature, 2);
        SignEd25519(privateKey, Blake2b512(content)).CopyTo(fileSignature, 2 + KeyIdLength);

        var trustedComment = FormattableString.Invariant(
            $"timestamp:{timestamp.ToUnixTimeSeconds()}\tfile:{fileName}\tprehashed");
        var trusted = System.Text.Encoding.UTF8.GetBytes(trustedComment);
        var globalMessage = new byte[SignatureLength + trusted.Length];
        fileSignature[(2 + KeyIdLength)..].CopyTo(globalMessage, 0);
        trusted.CopyTo(globalMessage, SignatureLength);
        var globalSignature = SignEd25519(privateKey, globalMessage);

        return string.Join('\n',
            $"untrusted comment: signature from AP Reelume release key",
            Convert.ToBase64String(fileSignature),
            $"trusted comment: {trustedComment}",
            Convert.ToBase64String(globalSignature)) + "\n";
    }

    /// <summary>
    /// Creates a key pair: the public half in the format minisign publishes, the secret half in
    /// the release-signing format <see cref="Sign"/> reads. The caller decides where each lives;
    /// nothing here touches a disk.
    /// </summary>
    public static (string PublicKeyFile, string SecretKeyBase64) CreateKeyPair()
    {
        var keyId = new byte[KeyIdLength];
        RandomNumberGenerator.Fill(keyId);
        var seed = new byte[SeedLength];
        RandomNumberGenerator.Fill(seed);
        var privateKey = new Ed25519PrivateKeyParameters(seed);
        var publicKey = privateKey.GeneratePublicKey().GetEncoded();

        var publicBytes = new byte[2 + KeyIdLength + PublicKeyLength];
        publicBytes[0] = (byte)'E';
        publicBytes[1] = (byte)'d';
        keyId.CopyTo(publicBytes, 2);
        publicKey.CopyTo(publicBytes, 2 + KeyIdLength);

        var secretBytes = new byte[2 + KeyIdLength + SeedLength];
        secretBytes[0] = (byte)'E';
        secretBytes[1] = (byte)'d';
        keyId.CopyTo(secretBytes, 2);
        seed.CopyTo(secretBytes, 2 + KeyIdLength);

        var publicFile = "untrusted comment: AP Reelume release signing public key\n"
            + Convert.ToBase64String(publicBytes) + "\n";
        return (publicFile, Convert.ToBase64String(secretBytes));
    }

    private static byte[] Blake2b512(ReadOnlySpan<byte> content)
    {
        var digest = new Blake2bDigest(512);
        digest.BlockUpdate(content);
        var hash = new byte[digest.GetDigestSize()];
        digest.DoFinal(hash, 0);
        return hash;
    }

    private static bool VerifyEd25519(Ed25519PublicKeyParameters key, byte[] message, byte[] signature)
    {
        var verifier = new Ed25519Signer();
        verifier.Init(forSigning: false, key);
        verifier.BlockUpdate(message, 0, message.Length);
        return verifier.VerifySignature(signature);
    }

    private static byte[] SignEd25519(Ed25519PrivateKeyParameters key, byte[] message)
    {
        var signer = new Ed25519Signer();
        signer.Init(forSigning: true, key);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }
}
