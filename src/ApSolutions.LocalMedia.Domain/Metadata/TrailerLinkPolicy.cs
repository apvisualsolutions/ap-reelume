// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Domain.Metadata;

/// <summary>
/// Turns the identifier a metadata provider sent into the one address this application is willing
/// to hand a browser, or into nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// The key is checked before anything is composed. Composing first and inspecting the result
/// afterwards leaves a malformed address in existence, and from then on every reader has to
/// remember to distrust it; refusing to build one means there is nothing to distrust. Measured
/// against the version that composed without checking, this refuses fifteen shapes that version
/// accepted — among them a whole <c>https://</c> address and a <c>javascript:</c> scheme smuggled
/// in as the value of <c>v</c>.
/// </para>
/// <para>
/// The rule is <c>^[A-Za-z0-9_-]{11}$</c>, spelled out here rather than handed to a regular
/// expression engine: eleven characters is the entire contract, and the ASCII predicates say so
/// without a pattern to misread. Note <see cref="char.IsAsciiLetterOrDigit"/> rather than
/// <c>char.IsLetterOrDigit</c> — the latter accepts digits from every script Unicode knows, and a
/// key is not a number, it is eleven bytes of a fixed alphabet.
/// </para>
/// <para>
/// The trailer is not played inside the application. Doing so through LibVLC would break YouTube's
/// terms, and their official embed would need a web view, hosts this application does not declare,
/// advertising and cookies. The browser is the use those terms allow, and this is where an address
/// for it comes from.
/// </para>
/// </remarks>
public static class TrailerLinkPolicy
{
    private const int KeyLength = 11;

    /// <summary>
    /// The watch address for a well-formed key, or <see langword="null"/> for anything else.
    /// </summary>
    public static string? TryBuildWatchLink(string? trailerKey) =>
        IsWellFormedKey(trailerKey) ? $"https://www.youtube.com/watch?v={trailerKey}" : null;

    private static bool IsWellFormedKey(string? trailerKey)
    {
        if (trailerKey is null || trailerKey.Length != KeyLength)
        {
            return false;
        }

        foreach (var character in trailerKey)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
