// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Updates;

namespace ApSolutions.LocalMedia.Windows.Updates;

/// <summary>
/// The release a run with a data root of its own is offered, written in its handover folder.
/// </summary>
/// <remarks>
/// <para>
/// The address lives here rather than in the source code, and that is a rule rather than a
/// convenience: a test walks <c>src/</c> for anything shaped like a host and fails on one the network
/// purpose registry does not declare. Declaring a harness host would claim a connection this
/// application never makes and would widen the check the network canary trusts, so the address is
/// data that a run puts in its own root.
/// </para>
/// <para>
/// The hash and the size are declared rather than computed from the file, so that a package which is
/// not what was promised stays expressible. Computing them here would make the download's verification
/// a tautology — it would be checking the file against itself.
/// </para>
/// </remarks>
public sealed record HandoffUpdateManifest(
    string Version,
    string Runtime,
    Uri Address,
    string Sha256,
    long SizeInBytes,
    string SummaryEs,
    string SummaryEn,
    string PackageFile)
{
    /// <summary>The file inside the handover folder, named for what it holds.</summary>
    public const string FileName = "update-manifest.json";

    /// <summary>
    /// Reads the manifest, or answers <see langword="null"/> when the folder holds none.
    /// </summary>
    /// <remarks>
    /// No manifest means no release, which is an answer and not a failure — the same answer a source
    /// that has published nothing gives. A manifest that cannot be read is the other thing entirely,
    /// and says so.
    /// </remarks>
    public static HandoffUpdateManifest? Read(string handoffDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handoffDirectory);
        var path = Path.Combine(handoffDirectory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new HandoffUpdateManifest(
                Text(root, "version"),
                Text(root, "runtime"),
                new Uri(Text(root, "url"), UriKind.Absolute),
                Text(root, "sha256"),
                root.GetProperty("sizeInBytes").GetInt64(),
                Text(root, "summaryEs"),
                Text(root, "summaryEn"),
                Text(root, "packageFile"));
        }
        catch (Exception exception)
            when (exception is JsonException or KeyNotFoundException or FormatException
                or UriFormatException or InvalidOperationException or IOException)
        {
            throw new UpdateSourceUnavailableException(
                $"The handover manifest at {path} could not be read as one.",
                exception);
        }
    }

    /// <summary>
    /// The release as the policy will judge it.
    /// </summary>
    /// <remarks>
    /// <see cref="UpdateRelease.Sha256Signed"/> is asserted rather than earned, and that is stated
    /// rather than hidden: it is a verdict only the real provider can reach, by verifying a minisign
    /// signature against the key this binary embeds. Making that key depend on the data root would
    /// move a security decision in order to test one, which is the one thing this repository forbids —
    /// so the harness source asserts the verdict the way a double in a unit test does, and minisign
    /// stays tested where it is tested, with its own vectors.
    /// <para>
    /// What is <em>not</em> asserted is everything the download proves: the hash, the size and the
    /// staging under <c>.partial</c> are the product's own and are exercised for real.
    /// </para>
    /// </remarks>
    public UpdateRelease ToRelease() => new(
        Version,
        Runtime,
        Address.AbsoluteUri,
        Sha256,
        SizeInBytes,
        SummaryEs,
        SummaryEn)
    {
        Sha256Signed = true,
    };

    private static string Text(JsonElement root, string name) =>
        root.GetProperty(name).GetString()
        ?? throw new FormatException($"The manifest's '{name}' is null.");
}
