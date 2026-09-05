// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The figures the living documents quote have to match what the tree actually measures.
/// </summary>
/// <remarks>
/// Written the 2026-08-31 after finding <b>three false figures in one day</b>, one of them inside the
/// very paragraph that warns about this: `CLAUDE.md` said the coverage ratchet stood at 205 while the
/// script said 191; the closing skill said a CI run takes 55-80 minutes when the measured figure was
/// already 42-53; and the guide said «las 48 vistas» in three places while the tree held 59 by the
/// project's own definition of a view. Nobody had noticed any of them, and `.claude/` was under no
/// gate at all.
/// <para>
/// <b>Two findings shaped this, and both are why it is a MARK rather than a scanner.</b> The first is
/// that scanning for bare numbers cannot work: «96» appears 272 times across these documents and
/// almost none of them is the coverage bar. The second is that most numbers in this repository are
/// <b>history and must not be checked</b> — the changelog and everything under `docs/evidence/` are
/// minutes of what was measured on a given day, so «las 48 vistas» is <i>correct</i> there for ever.
/// Only documents that assert the present are read here.
/// </para>
/// <para>
/// So a figure opts in by carrying <c>&lt;!--medido:clave--&gt;</c> after it, and the mark names a
/// source in <see cref="Sources"/>. The failure names the file, the line, what it claims and what the
/// source measures, because a gate that only says «mismatch» sends somebody hunting.
/// </para>
/// <para>
/// <b>It is deliberately small.</b> Six sources, not a general system: a gate covering the figures
/// that are actually quoted is worth more than a framework nobody maintains. Adding a source is one
/// entry and one mark.
/// </para>
/// </remarks>
public sealed class QuotedFigureTests
{
    /// <summary>
    /// Documents that describe the tree <b>as it is now</b>. The changelog, the evidence and the
    /// handover are excluded on purpose: they are dated records, and holding a record to today's
    /// measurement would be asking history to change.
    /// </summary>
    private static readonly string[] LivingDocuments =
    [
        "CLAUDE.md",
        "CONTRIBUTING.md",
        // The scope matrix, added on 2026-09-06 because it was the one document where a stale figure
        // cost the most and nothing was watching. PRD-006's acceptance criterion promised "the 53
        // views" while the tree measured 61: the 53 came from counting only the files whose name
        // ends in View, and eight surfaces do not. A criterion cannot be closed honestly against a
        // census that no longer describes it, so the figure now carries its mark like the rest.
        "docs/FEATURES.md",
    ];

    /// <summary>
    /// What each mark is checked against. The key is what appears in the document; the value reads
    /// the tree the same way the tool that owns the figure does.
    /// </summary>
    private static readonly Dictionary<string, Func<int>> Sources = new(StringComparer.Ordinal)
    {
        // The ratchet the coverage gate enforces, and the list it has to agree with. They are two
        // entries rather than one because the whole point of the pair is that they can disagree.
        ["trinquete-de-deuda"] = () => Single(
            RepositoryLayout.PathFromRoot("eng/check-coverage.ps1"),
            @"\$debtRatchet\s*=\s*(?<value>\d+)"),
        ["archivos-en-deuda"] = () => CountLines(
            RepositoryLayout.PathFromRoot("eng/coverage-debt.txt"),
            line => line.StartsWith("src/", StringComparison.Ordinal)),
        ["paseo-pendiente"] = () => CountLines(
            RepositoryLayout.PathFromRoot("eng/walk-pending.txt"),
            line => line.Length > 0 && !line.StartsWith('#')),

        // A view is decided by its root element, which is the same rule LeadingActionTests uses. A
        // second definition of "view" would be a second answer to one question.
        ["vistas"] = () => Directory
            .EnumerateFiles(RepositoryLayout.PathFromRoot("src"), "*.axaml", SearchOption.AllDirectories)
            .Count(path => Regex.IsMatch(
                File.ReadAllText(path),
                @"<(UserControl|Window)\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(2))),
        ["identificadores-de-alcance"] = () => CountLines(
            RepositoryLayout.PathFromRoot("docs/FEATURES.md"),
            line => Regex.IsMatch(
                line,
                @"^\|\s*[A-Z][A-Z0-9]{1,4}-[0-9]{3}\s*\|",
                RegexOptions.None,
                TimeSpan.FromSeconds(2))),
        ["listones-de-cobertura"] = () => (int)Math.Round(
            double.Parse(
                Match(
                    RepositoryLayout.PathFromRoot("eng/check-coverage.ps1"),
                    @"MinimumLinePercent\s*=\s*(?<value>[\d.]+)"),
                System.Globalization.CultureInfo.InvariantCulture)),
    };

    /// <summary>The number that carries the mark, taken from the same line, immediately before it.</summary>
    private static readonly Regex Marked = new(
        @"(?<value>\d+)(?:\*\*)?[^\d\n]{0,20}?<!--\s*medido:(?<key>[a-z0-9-]+)\s*-->",
        RegexOptions.None,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_marked_figure_matches_what_the_tree_measures()
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (document, lineNumber, line) in MarkedLines())
        {
            foreach (Match match in Marked.Matches(line))
            {
                var key = match.Groups["key"].Value;
                var claimed = int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
                seen.Add(key);

                if (!Sources.TryGetValue(key, out var source))
                {
                    // An unknown key is not skipped: a typo would silently switch the check off,
                    // which is the failure mode this whole file exists to remove.
                    problems.Add($"{document}:{lineNumber} marks 'medido:{key}', which no source defines.");
                    continue;
                }

                var measured = source();
                if (claimed != measured)
                {
                    problems.Add(
                        $"{document}:{lineNumber} says {claimed} for 'medido:{key}', and the tree measures {measured}." +
                        $"{Environment.NewLine}    {line.Trim()}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));

        // Anti-blindness floor. A regex that stopped matching would leave this passing while checking
        // nothing at all, which is exactly how the figures went stale in the first place.
        Assert.True(
            seen.Count >= 4,
            $"Only {seen.Count} marked figures were found, so this gate is measuring almost nothing. " +
            "Either the marks were removed or the pattern stopped matching them.");
    }

    /// <summary>
    /// A source nobody quotes is dead weight, and dead weight is how a list stops being read. This is
    /// the same rule the orphan-service list already holds the container to.
    /// </summary>
    [Fact]
    public void Every_source_is_quoted_by_at_least_one_document()
    {
        var quoted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, _, line) in MarkedLines())
        {
            foreach (Match match in Marked.Matches(line))
            {
                quoted.Add(match.Groups["key"].Value);
            }
        }

        var unquoted = Sources.Keys.Where(key => !quoted.Contains(key)).OrderBy(key => key, StringComparer.Ordinal);
        Assert.True(
            !unquoted.Any(),
            "These sources are registered and quoted by nobody; either mark a figure with them or " +
            $"remove them: {string.Join(", ", unquoted)}");
    }

    private static IEnumerable<(string Document, int Line, string Text)> MarkedLines()
    {
        var documents = LivingDocuments
            .Select(relative => Path.Combine(RepositoryLayout.Root, relative))
            .Concat(Directory.EnumerateFiles(
                RepositoryLayout.PathFromRoot(".claude"),
                "*.md",
                SearchOption.AllDirectories))
            .Where(File.Exists)

            // NOT the worktrees, which are other sessions' checkouts of this same repository sitting
            // inside it and ignored by git. Measured on 2026-09-03, when three sessions were started
            // in parallel: each one holds its own CLAUDE.md at whatever commit it began from, so a
            // figure this batch had just corrected read as wrong from three copies at once and put
            // this gate red over work nobody had done. A gate that fails because somebody else is
            // working is a gate that teaches people to ignore it.
            //
            // RELATIVE TO THE ROOT AND NEVER ABSOLUTE, which the first version got wrong and a
            // parallel session measured within the hour. Matched against the absolute path, a
            // checkout that itself lives under «.claude/worktrees/» excludes EVERY ONE of its own
            // documents: the sweep reads nothing, and the anti-blindness floor below fires — so the
            // gate went red for three of the four sessions and green in CI, where the checkout sits
            // somewhere else. It failed by its LOCATION rather than by its content, which is the
            // same defect it was written to fix, pointed at itself.
            .Where(path => !Path.GetRelativePath(RepositoryLayout.Root, path)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Contains(".claude/worktrees/", StringComparison.Ordinal));

        foreach (var path in documents)
        {
            var relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                yield return (relative, index + 1, lines[index]);
            }
        }
    }

    private static int CountLines(string path, Func<string, bool> predicate) =>
        File.Exists(path) ? File.ReadAllLines(path).Count(predicate) : -1;

    private static int Single(string path, string pattern) =>
        int.Parse(Match(path, pattern), System.Globalization.CultureInfo.InvariantCulture);

    private static string Match(string path, string pattern)
    {
        var match = Regex.Match(
            File.ReadAllText(path),
            pattern,
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, $"The source pattern '{pattern}' matched nothing in {path}.");
        return match.Groups["value"].Value;
    }
}
