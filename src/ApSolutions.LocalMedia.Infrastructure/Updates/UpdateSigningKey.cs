// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Infrastructure.Updates;

/// <summary>
/// The public half of the release-signing key, embedded in the binary (SEC-003).
/// </summary>
/// <remarks>
/// Every release signs its <c>SHA256SUMS.txt</c> with the private half, which lives outside this
/// repository — a GitHub Actions secret and the owner's guarded copy. The updater refuses any
/// release whose checksums this key did not sign, which is what stops the expected hash from
/// coming out of the same unsigned answer as the package it vouches for. Rotating the key means
/// shipping a version that embeds the new public half; the file copy in
/// <c>eng/release-signing.pub</c> is the same value for people verifying by hand with minisign.
/// </remarks>
public static class UpdateSigningKey
{
    public const string PublicKey = "RWRQ2KXP9uXn7h101YR8sVrCc4DzWuj+aVUiWAhQRy7jZ/bW+KOFGHHb";
}
