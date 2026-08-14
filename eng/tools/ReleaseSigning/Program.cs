// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Updates;

// The release-signing tool: makes the key pair, signs SHA256SUMS.txt, and verifies a signature the
// way the shipped updater will. The secret key is read from a file or an environment variable and
// is never printed; where it lives is the owner's decision, and this repository is not a place it
// may live.
return args switch
{
    ["keygen", var publicPath, var secretPath] => Keygen(publicPath, secretPath),
    ["sign", var file, var secretSource] => Sign(file, secretSource),
    ["verify", var file, var signaturePath, var publicPath] => Verify(file, signaturePath, publicPath),
    _ => Usage(),
};

static int Keygen(string publicPath, string secretPath)
{
    if (File.Exists(publicPath) || File.Exists(secretPath))
    {
        Console.Error.WriteLine("Refusing to overwrite an existing key. Move it away first, deliberately.");
        return 1;
    }

    var (publicFile, secretBase64) = Minisign.CreateKeyPair();
    File.WriteAllText(publicPath, publicFile);
    File.WriteAllText(secretPath, secretBase64 + Environment.NewLine);
    Console.WriteLine($"Public key:  {publicPath}");
    Console.WriteLine($"Secret key:  {secretPath}  — keep it OUTSIDE every repository, and back it up.");
    return 0;
}

static int Sign(string file, string secretSource)
{
    var secretText = File.Exists(secretSource)
        ? File.ReadAllText(secretSource)
        : Environment.GetEnvironmentVariable(secretSource);
    if (string.IsNullOrWhiteSpace(secretText))
    {
        Console.Error.WriteLine($"No secret key at '{secretSource}' (file or environment variable).");
        return 1;
    }

    var signature = Minisign.Sign(
        Convert.FromBase64String(secretText.Trim()),
        File.ReadAllBytes(file),
        Path.GetFileName(file),
        DateTimeOffset.UtcNow);
    File.WriteAllText(file + ".minisig", signature);
    Console.WriteLine($"Signed: {file}.minisig");
    return 0;
}

static int Verify(string file, string signaturePath, string publicPath)
{
    if (!Minisign.TryParseSignature(File.ReadAllText(signaturePath), out var signature))
    {
        Console.Error.WriteLine($"'{signaturePath}' is not a minisign signature.");
        return 1;
    }

    var valid = Minisign.Verify(
        Minisign.ParsePublicKey(File.ReadAllText(publicPath)),
        File.ReadAllBytes(file),
        signature!);
    Console.WriteLine(valid ? "Signature OK." : "SIGNATURE DOES NOT VERIFY.");
    return valid ? 0 : 1;
}

static int Usage()
{
    Console.Error.WriteLine("usage: ReleaseSigning keygen <public-out> <secret-out>");
    Console.Error.WriteLine("       ReleaseSigning sign <file> <secret-file-or-env-name>");
    Console.Error.WriteLine("       ReleaseSigning verify <file> <signature> <public-key>");
    return 2;
}
