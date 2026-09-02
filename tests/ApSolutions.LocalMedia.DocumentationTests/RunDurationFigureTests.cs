// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// How long a CI run takes is quoted in four places, and they have to say the same thing.
/// </summary>
/// <remarks>
/// The figure was measured once, on 2026-08-30, over the twelve complete runs of that day: 42.7
/// minutes for the fastest and 52.6 for the slowest. Nothing in the tree can re-measure it, which is
/// exactly why it rots — and it did. On 2026-09-02 the same fact was written three different ways:
/// <c>CLAUDE.md</c> and the closing skill said 42-53, <c>.claude/hooks/post-push.sh</c> said 42-50,
/// and <c>eng/watch-ci.ps1</c> still said 55-80. The last one had been corrected in the other two
/// places on 2026-08-31 by somebody who did not think to look outside <c>*.md</c>.
/// <para>
/// <b>What this gate is, said plainly, because a gate that seems to check more than it does is
/// worse than none.</b> It cannot tell whether 42-53 is true; it can only tell whether the tree
/// agrees with itself. Four copies saying 55-80 would pass. What it removes is the failure that
/// actually happened twice — one copy corrected and the others left behind.
/// </para>
/// <para>
/// The one thing it does check against something real is the script's own defaults, which its
/// docstring says are set from this figure: the heartbeat has to fire before even the fastest run
/// ends, or a healthy run is silent, and the ceiling has to sit above the slowest, or the watcher
/// gives up on runs that were going to finish.
/// </para>
/// <para>
/// It sweeps rather than listing files, so a fifth place that quotes the figure is covered the day
/// it is written. <c>docs/</c> is deliberately outside the sweep: the changelog, the evidence and
/// the handover are dated records, and 55-80 is correct in them for ever.
/// </para>
/// <para>
/// <b>Its first version was wrong in two ways, and neither was visible from inside a worktree.</b>
/// Run from the main checkout it went red, naming ten quotations: it swept
/// <c>.claude/worktrees/</c> — whole copies of the repository belonging to other sessions, absent
/// from a CI runner — and it counted a changelog line recording the old figure as though it were
/// claiming it. So it was <b>red locally and green in CI</b>, and it forbade documents from saying
/// what the number used to be, inside the very change that corrected it. Both are fixed above:
/// other checkouts are skipped, and only sentences with a present-tense verb are read.
/// </para>
/// <para>
/// <b>And the fix was measured against the defect the gate exists for, not only against the noise.</b>
/// Reverting <c>eng/watch-ci.ps1</c> to its line in <c>3cdeeb3</c> — «A run in this repository takes
/// 55-80 minutes», an assertion in the present tense that contradicted the measured figure — puts
/// the gate red again, naming file, line and value. That is the case a narrower pattern could
/// easily have lost, which would have been repairing the false positive by breaking the true one.
/// </para>
/// </remarks>
public sealed class RunDurationFigureTests
{
    /// <summary>
    /// "run", a verb in the present tense, then the range and its unit — with no full stop anywhere
    /// between them, which is the fence that stops it stitching the word in one sentence to a pair
    /// of numbers in the next.
    /// </summary>
    /// <remarks>
    /// <b>The verb is what separates a claim from a quotation, and it was missing at first.</b>
    /// Without it the pattern also caught "esa cifra decía 55-80 minutos" — a changelog or a guide
    /// recording that the figure used to say something else. A document has to be able to name the
    /// old number; that is what a correction is made of. Requiring "takes"/"tarda" reads only
    /// sentences that assert the duration now, and every one of the four live quotations is
    /// phrased that way. Measured 2026-09-02: four matches, all of them the figure, and none of the
    /// neighbouring ranges — 33-55 for the Verify step, dates like 2026-09-02, the decimals 42.7
    /// and 52.6 — came back with them.
    /// <para>
    /// The cost is the usual one for a narrower pattern: "un run dura 42-53 min" is covered,
    /// "un run se va a 42-53 min" is not. That is what the anti-blindness floor below is for.
    /// </para>
    /// </remarks>
    private static readonly Regex Quoted = new(
        @"\brun\b[^.]{0,40}?\b(?:takes|tarda|lasts|dura)\b[^.]{0,20}?\b(?<fast>\d{2})-(?<slow>\d{2})\s*min",
        RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(5));

    [Fact]
    public void Every_place_that_says_how_long_a_run_takes_says_the_same_thing()
    {
        var quotes = Quotations();

        Assert.True(
            quotes.Count >= 3,
            $"Only {quotes.Count} place(s) quote how long a run takes, and there were four. Either "
            + "the figure was removed from most of them or the pattern stopped matching it, and a "
            + "gate that matches nothing passes for ever.");

        var distinct = quotes
            .Select(quote => quote.Range)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(range => range, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            distinct.Length == 1,
            $"The tree quotes {distinct.Length} different durations for one run ({string.Join(", ", distinct)}). "
            + "One of them is stale, and there is no way to tell which from the outside:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                quotes.Select(quote => $"    {quote.Document}:{quote.Line} says {quote.Range}")));
    }

    /// <summary>
    /// The watcher's defaults are set from this figure, so the figure and the defaults have to stay
    /// compatible: a heartbeat that only fires after the fastest run would leave a healthy run
    /// looking like a stuck one, and a ceiling below the slowest would give up on runs that finish.
    /// </summary>
    [Fact]
    public void The_watcher_defaults_still_bracket_the_duration_they_were_set_from()
    {
        var quote = Quotations()[0];
        var script = File.ReadAllText(RepositoryLayout.PathFromRoot("eng/watch-ci.ps1"));

        var heartbeat = DefaultOf(script, "HeartbeatMinutes");
        var ceiling = DefaultOf(script, "TimeoutMinutes");

        Assert.True(
            heartbeat < quote.Fastest,
            $"The heartbeat is every {heartbeat} min and even the fastest run takes {quote.Fastest}, "
            + "so a run that is perfectly healthy says nothing at all until it is over — which is "
            + "the silence this script exists to remove.");
        Assert.True(
            ceiling > quote.Slowest,
            $"The ceiling is {ceiling} min and the slowest run takes {quote.Slowest}, so the watcher "
            + "gives up on runs that were going to finish.");
    }

    /// <summary>
    /// The sweep reads this checkout and nothing else, so it gives the same answer here and on a
    /// runner. Asserted against a fabricated path as well as the real sweep, because in CI there
    /// are no other checkouts and a test that only looked at the real sweep would pass there
    /// without checking anything — which is the exact shape of the defect it guards against.
    /// </summary>
    [Fact]
    public void The_sweep_never_reads_another_sessions_checkout()
    {
        Assert.True(
            IsInsideAnotherCheckout(RepositoryLayout.PathFromRoot(".claude/worktrees/other/CLAUDE.md")),
            "A path inside .claude/worktrees is another session's copy of this repository and must be skipped.");
        Assert.False(
            IsInsideAnotherCheckout(RepositoryLayout.PathFromRoot(".claude/skills/cerrar-tanda/SKILL.md")),
            "The exclusion has grown wide enough to skip this repository's own files.");

        var trespassing = Documents()
            .Where(IsInsideAnotherCheckout)
            .ToArray();
        Assert.True(
            trespassing.Length == 0,
            "The sweep is reading other checkouts, so its answer depends on how many sessions are "
            + $"open: {string.Join(", ", trespassing)}");
    }

    /// <summary>
    /// A document has to be able to say what the figure used to be — that is what a correction is
    /// made of — so only sentences that assert the duration now are read. Held with the very
    /// sentence that broke it: CLAUDE.md records that this figure said 55-80 until it was measured.
    /// </summary>
    [Fact]
    public void Recording_what_the_figure_used_to_say_is_not_a_contradiction()
    {
        Assert.DoesNotMatch(Quoted, "Esa cifra de un run decía 55-80 minutos hasta que se midió.");
        Assert.DoesNotMatch(Quoted, "The docstring said a run of 55-80 minutes until it was measured.");

        // And the pattern is still awake: the same sentence in the present tense is caught.
        Assert.Matches(Quoted, "Un run tarda 55-80 minutos.");
        Assert.Matches(Quoted, "A run in this repository takes 55-80 minutes.");
    }

    private static int DefaultOf(string script, string parameter)
    {
        var match = Regex.Match(
            script,
            @"\[int\]\$" + parameter + @"\s*=\s*(?<value>\d+)",
            RegexOptions.None,
            TimeSpan.FromSeconds(5));
        Assert.True(match.Success, $"eng/watch-ci.ps1 no longer declares a default for -{parameter}.");
        return int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static List<Quotation> Quotations()
    {
        var found = new List<Quotation>();
        foreach (var path in Documents())
        {
            var relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
            var text = File.ReadAllText(path);
            foreach (Match match in Quoted.Matches(text))
            {
                var fast = int.Parse(match.Groups["fast"].Value, System.Globalization.CultureInfo.InvariantCulture);
                var slow = int.Parse(match.Groups["slow"].Value, System.Globalization.CultureInfo.InvariantCulture);
                var line = text.Take(match.Index).Count(character => character == '\n') + 1;
                found.Add(new Quotation(relative, line, $"{fast}-{slow}", fast, slow));
            }
        }

        return found;
    }

    private static IEnumerable<string> Documents()
    {
        yield return RepositoryLayout.PathFromRoot("CLAUDE.md");
        yield return RepositoryLayout.PathFromRoot("CONTRIBUTING.md");

        foreach (var path in Directory.EnumerateFiles(
            RepositoryLayout.PathFromRoot(".claude"),
            "*.*",
            SearchOption.AllDirectories))
        {
            if (IsInsideAnotherCheckout(path))
            {
                continue;
            }

            // The hooks quote it too, and the hooks are where the stale copy was hiding: a sweep
            // that only reads *.md would have passed on the day this was written.
            if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }

        foreach (var path in Directory.EnumerateFiles(
            RepositoryLayout.PathFromRoot("eng"),
            "*.ps1",
            SearchOption.TopDirectoryOnly))
        {
            yield return path;
        }
    }

    /// <summary>
    /// Whether a path belongs to a different checkout of this repository rather than to this one.
    /// </summary>
    /// <remarks>
    /// <c>.claude/worktrees/</c> holds whole copies of the repository belonging to other sessions.
    /// It is excluded from version control in <c>.git/info/exclude</c>, and a CI runner does not
    /// have it at all — so a sweep that reads it answers a different question depending on how many
    /// sessions happen to be open and what each of them is holding in its tree. On 2026-09-02 that
    /// made this gate <b>red on the machine of whoever was writing and green in CI</b>: it found two
    /// other checkouts, one of them carrying changelog lines that record the old 55-80 figure. A
    /// gate that only fails locally is one people learn to ignore, and a gate people ignore has
    /// stopped guarding.
    /// </remarks>
    private static bool IsInsideAnotherCheckout(string path) =>
        Path.GetRelativePath(RepositoryLayout.Root, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("worktrees", StringComparison.OrdinalIgnoreCase));

    private sealed record Quotation(string Document, int Line, string Range, int Fastest, int Slowest);
}
