// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.UiTests.Shell;

/// <summary>
/// Whether one surface names another, read from what the file says rather than from what it once
/// said. Used by <see cref="SurfaceReachabilityTests"/> to build the graph it walks, and separated
/// from it so the reading itself can be measured (ARQ-013).
/// </summary>
internal static class SurfaceReferences
{
    private static readonly TimeSpan MatchCeiling = TimeSpan.FromSeconds(1);

    /// <summary>A surface instantiates another when its markup opens that element under any prefix.</summary>
    public static bool InMarkup(string markup, string candidate) =>
        markup.Length > 0
        && Regex.IsMatch(
            WithoutMarkupComments(markup),
            $@"<[A-Za-z0-9_]+:{Regex.Escape(candidate)}[\s/>]",
            RegexOptions.None,
            MatchCeiling);

    /// <summary>A code-behind reaches a surface when it names its type.</summary>
    public static bool InCode(string code, string candidate) =>
        code.Length > 0
        && Regex.IsMatch(
            WithoutCodeComments(code),
            $@"\b{Regex.Escape(candidate)}\b",
            RegexOptions.None,
            MatchCeiling);

    /// <summary>Drops every <c>&lt;!-- … --&gt;</c>, the licence header included.</summary>
    private static string WithoutMarkupComments(string markup) =>
        Regex.Replace(markup, "<!--.*?-->", " ", RegexOptions.Singleline, MatchCeiling);

    /// <summary>
    /// Drops <c>/* … */</c> and <c>// …</c>. The line form is guarded against <c>://</c> so a scheme
    /// inside a string does not swallow the rest of the line. Nothing further is attempted: trimming
    /// too much loses a reference and produces an orphan, which fails loudly, and can never invent
    /// reachability — so the safe direction is the one this errs towards.
    /// </summary>
    private static string WithoutCodeComments(string code) =>
        Regex.Replace(
            Regex.Replace(code, @"/\*.*?\*/", " ", RegexOptions.Singleline, MatchCeiling),
            @"(?<!:)//[^\r\n]*",
            " ",
            RegexOptions.None,
            MatchCeiling);
}
