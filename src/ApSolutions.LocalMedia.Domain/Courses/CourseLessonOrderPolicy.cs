// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.Domain.Courses;

/// <summary>
/// The number a lesson carries at the head of its name, kept as an ordered pair (CRS-001).
/// </summary>
/// <remarks>
/// <see cref="Minor"/> is what stops <c>1.3 Título</c> from becoming <c>1 3 Título</c>, which is what
/// the existing name cleaner does to it today: a hierarchical number is one number in two parts and
/// destroying the dot destroys the order as well as the title.
/// </remarks>
public readonly record struct LessonOrdinal(int Major, int? Minor)
{
    /// <summary>
    /// What this sorts by. A missing minor part sorts as -1 so that a lesson comes before its own
    /// subdivisions — <c>1 Intro</c> then <c>1.1 Setup</c> — and it is a tuple rather than an
    /// <see cref="IComparable{T}"/> so the struct does not have to carry four comparison operators
    /// that only the sort would ever call.
    /// </summary>
    internal (int Major, int Minor) SortKey => (Major, Minor ?? -1);
}

/// <summary>
/// What a lesson's file name says about where it goes (CRS-001, ADR-0006 decision 4).
/// </summary>
/// <remarks>
/// Ordering is numeric and not alphabetical, because alphabetical puts <c>10</c> before <c>2</c> and
/// a course watched in that order is a course watched wrong. The four shapes read here —
/// <c>NN - title</c>, <c>NN-title</c>, <c>NN. title</c> and <c>NN_title</c> — are the ones measured
/// over a real collection of 595 lessons, where they cover 80.8 % of the names.
/// <para>
/// The remaining 19.2 % is <b>not unnumbered material</b>, and treating it as noise would have been
/// the mistake: it carries its numbering elsewhere, in encoded schemes of the
/// <c>XX_NNN_SS_LL</c> kind and in parentheses mid-name. Those are zero-padded, so alphabetical order
/// agrees with numeric order for them, and sorting them last alphabetically and stably puts them
/// right without a pattern anybody has to maintain.
/// </para>
/// <para>
/// A leading run of digits is read as a number only up to three of them. Four is a year, and a
/// lesson called <c>2019 - Something</c> is a lesson whose title starts with a year rather than a
/// lesson numbered two thousand and nineteen. That limit is why this policy does not reproduce the
/// false positive the film name parser produces on the same collection.
/// </para>
/// </remarks>
public static class CourseLessonOrderPolicy
{
    private static readonly TimeSpan MatchBudget = TimeSpan.FromSeconds(1);

    /// <summary>Hierarchical first, or <c>1.3 Título</c> reads as lesson 1 titled "3 Título".</summary>
    private static readonly Regex Hierarchical = new(
        @"^\s*(?<major>\d{1,3})\.(?<minor>\d{1,3})(?:\s*[-–—_.]\s*|\s+)(?<rest>\S.*)$",
        RegexOptions.CultureInvariant,
        MatchBudget);

    private static readonly Regex Dashed = new(
        @"^\s*(?<major>\d{1,3})\s*[-–—]\s*(?<rest>\S.*)$",
        RegexOptions.CultureInvariant,
        MatchBudget);

    private static readonly Regex Dotted = new(
        @"^\s*(?<major>\d{1,3})\.\s+(?<rest>\S.*)$",
        RegexOptions.CultureInvariant,
        MatchBudget);

    private static readonly Regex Underscored = new(
        @"^\s*(?<major>\d{1,3})_\s*(?<rest>\S.*)$",
        RegexOptions.CultureInvariant,
        MatchBudget);

    /// <summary>
    /// The number at the head of <paramref name="name"/>, or <see langword="null"/> when it carries
    /// none. <paramref name="name"/> is a file or folder name with no extension on it.
    /// </summary>
    public static LessonOrdinal? ReadOrdinal(string? name) => Read(name)?.Ordinal;

    /// <summary>
    /// What is left of <paramref name="name"/> once its leading number and separator are taken off.
    /// A name that carries no number is its own title, trimmed and never emptied.
    /// </summary>
    public static string ReadTitle(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        return Read(trimmed)?.Title ?? trimmed;
    }

    /// <summary>
    /// <paramref name="items"/> in the order a course is watched: numbered first and by their
    /// number, then everything unnumbered, alphabetically. Both groups break ties on the name, so
    /// the answer never depends on the order the file system happened to hand them over in.
    /// </summary>
    public static IReadOnlyList<T> Order<T>(IEnumerable<T> items, Func<T, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(nameOf);

        return items
            .Select(item => (Item: item, Name: nameOf(item) ?? string.Empty))
            .Select(entry => (entry.Item, entry.Name, Ordinal: ReadOrdinal(entry.Name)))
            .OrderBy(entry => entry.Ordinal is null)
            .ThenBy(entry => entry.Ordinal?.SortKey ?? default)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Select(entry => entry.Item)
            .ToArray();
    }

    private static (LessonOrdinal Ordinal, string Title)? Read(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (Hierarchical.Match(name) is { Success: true } hierarchical)
        {
            return (
                new LessonOrdinal(Number(hierarchical, "major"), Number(hierarchical, "minor")),
                hierarchical.Groups["rest"].Value.Trim());
        }

        foreach (var pattern in new[] { Dashed, Dotted, Underscored })
        {
            if (pattern.Match(name) is { Success: true } match)
            {
                return (new LessonOrdinal(Number(match, "major"), null), match.Groups["rest"].Value.Trim());
            }
        }

        return null;
    }

    private static int Number(Match match, string group) =>
        int.Parse(match.Groups[group].Value, NumberStyles.None, CultureInfo.InvariantCulture);
}
