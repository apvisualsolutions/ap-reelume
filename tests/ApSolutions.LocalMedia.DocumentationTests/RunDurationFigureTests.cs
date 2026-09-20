// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Text.RegularExpressions;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// How long a CI run takes is written in exactly one place, and this is what keeps it there.
/// </summary>
/// <remarks>
/// The figure was measured once, on 2026-08-30, over the twelve complete runs of that day: 42.7
/// minutes for the fastest and 52.6 for the slowest. Nothing in the tree can re-measure it, which is
/// exactly why it rots — and it did. On 2026-09-02 the same fact was written three different ways:
/// <c>CLAUDE.md</c> and the closing skill said 42-53, <c>.claude/hooks/post-push.sh</c> said 42-50,
/// and <c>eng/watch-ci.ps1</c> still said 55-80. The last one had been corrected in the other two
/// places on 2026-08-31 by somebody who did not think to look outside <c>*.md</c>.
/// <para>
/// <b>This gate used to compare the copies against each other, and on 2026-09-12 the owner settled
/// what to do instead.</b> Copies that agree are not copies that are right — three saying 55-80
/// would have passed — and the figure moved four times in five days, so every correction was three
/// edits and a chance to miss one. It now lives in <c>eng/watch-ci.ps1</c> and nowhere else, because
/// that is the one place with work for it: the watcher's heartbeat and ceiling are set from it. The
/// gate below stops a second copy from ever appearing, which is the failure that actually happened,
/// twice.
/// </para>
/// <para>
/// <b>What it still cannot tell you is whether the figure is true.</b> One copy saying 55-80 would
/// pass. That question has an answer now and it is not a test:
/// <c>pwsh -NoProfile -File eng/measure-ci-time.ps1</c>.
/// </para>
/// <para>
/// The one thing it does check against something real is the script's own defaults, which its
/// docstring says are set from this figure: the heartbeat has to fire before even the fastest run
/// ends, or a healthy run is silent, and the ceiling has to sit above the slowest, or the watcher
/// gives up on runs that were going to finish.
/// </para>
/// <para>
/// It sweeps rather than listing files, so a second place that quotes the figure is caught the day
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
    /// sentences that assert the duration now, and the one live quotation left is
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

    /// <summary>The one place allowed to say it, because its own defaults are set from it.</summary>
    private const string TheOnlyPlace = "eng/watch-ci.ps1";

    [Fact]
    public void Only_the_watcher_says_how_long_a_run_takes()
    {
        var quotes = Quotations();

        Assert.True(
            quotes.Count > 0,
            $"Nothing in the tree says how long a run takes, and {TheOnlyPlace} has to: its "
            + "heartbeat and its ceiling are set from that figure, and a reader with neither the "
            + "number nor the reason cannot tell a healthy silence from a stuck one. Either the "
            + "figure was deleted or the pattern stopped matching it, and a gate that matches "
            + "nothing passes for ever.");

        Assert.True(
            quotes.Count == 1,
            $"{quotes.Count} places say how long a run takes, and there is room for one. The figure "
            + "moved four times in five days and every copy is a copy that can be left behind — "
            + $"which happened twice. Say it in {TheOnlyPlace} and point everywhere else at "
            + "eng/measure-ci-time.ps1, which measures it instead of remembering it:"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine,
                quotes.Select(quote => $"    {quote.Document}:{quote.Line} says {quote.Range}")));

        Assert.True(
            quotes[0].Document == TheOnlyPlace,
            $"The only place quoting the figure is {quotes[0].Document}, not {TheOnlyPlace}. It "
            + "belongs with the defaults it sets, or the next person to change one will not find "
            + "the other.");
    }

    /// <summary>
    /// The watcher's defaults are set from this figure, so the figure and the defaults have to stay
    /// compatible: a heartbeat that only fires after the fastest run would leave a healthy run
    /// looking like a stuck one, and a ceiling below the slowest would give up on runs that finish.
    /// </summary>
    [Fact]
    public void The_watcher_defaults_still_bracket_the_duration_they_were_set_from()
    {
        var quotes = Quotations();

        // Its own floor rather than its sister's: without this it dies on an index instead of
        // saying what is wrong, and if the surviving copy were somewhere else it would bracket the
        // watcher's settings against a figure the watcher does not carry.
        Assert.True(
            quotes.Count == 1 && quotes[0].Document == TheOnlyPlace,
            $"This brackets {TheOnlyPlace}'s own settings, so the figure has to be its own: found "
            + $"{quotes.Count} quotation(s), the first in "
            + $"{(quotes.Count == 0 ? "nowhere" : quotes[0].Document)}.");

        var quote = quotes[0];
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
            IsInsideAnotherCheckout(RepositoryLayout.PathFromRoot(".claude/skills/cierre/SKILL.md")),
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

    /// <summary>
    /// Where a stale copy of the figure could hide: prose, hooks, skills, scripts and workflow
    /// files, anywhere in the checkout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This used to say it swept and did not.</b> It read two files by name, then <c>.claude</c>
    /// filtered to <c>.md</c> and <c>.sh</c>, then <c>eng</c>'s top level filtered to <c>.ps1</c>.
    /// An audit on 2026-09-12 put a live present-tense copy in <c>README.es.md</c> and another in
    /// <c>eng/README-sandbox.md</c> and the gate stayed green on all four tests — with two stale
    /// second copies sitting in the tree. Everything outside those three roots was invisible,
    /// <c>.github/</c> and <c>.claude/settings.json</c> included, and settings.json is where two of
    /// this repository's hooks live inline.
    /// </para>
    /// <para>
    /// <b><c>.cs</c> is left out on purpose and it is the one hole left.</b> The pattern's own
    /// fixtures are C# string literals in this very file — «A run in this repository takes 55-80
    /// minutes» is asserted three lines below to prove the pattern is awake — so sweeping <c>.cs</c>
    /// would force the gate to exclude itself, and a gate with a hole shaped like its own filename
    /// is where the next stale copy goes. The figure has no business in compiled code.
    /// </para>
    /// <para>
    /// <c>docs/</c> stays out for the original reason: the changelog, the evidence and the handover
    /// are dated records, and 55-80 is correct in them for ever.
    /// </para>
    /// </remarks>
    private static IEnumerable<string> Documents()
    {
        string[] readable = [".md", ".sh", ".ps1", ".psm1", ".json", ".yml", ".yaml", ".txt"];
        string[] skipped = ["docs", ".git", "bin", "obj", "artifacts", "node_modules", "worktrees"];

        foreach (var path in Directory.EnumerateFiles(
            RepositoryLayout.Root,
            "*.*",
            SearchOption.AllDirectories))
        {
            var segments = Path.GetRelativePath(RepositoryLayout.Root, path)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Length > 1
                && segments[..^1].Any(segment => skipped.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (readable.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// Whether a path belongs to a different checkout of this repository rather than to this one.
    /// </summary>
    /// <remarks>
    /// The measurement that made this necessary was taken here on 2026-09-02 — two other checkouts,
    /// one carrying changelog lines recording the old 55-80 figure, so the gate was <b>red on the
    /// machine of whoever was writing and green in CI</b>. The rule itself moved onto
    /// <see cref="RepositoryLayout"/> with ENG-009, when a third sweep of this suite needed it and
    /// the tree was about to hold a fourth spelling of the same idea.
    /// </remarks>
    private static bool IsInsideAnotherCheckout(string path) =>
        RepositoryLayout.IsInsideAnotherCheckout(path);

    private sealed record Quotation(string Document, int Line, string Range, int Fastest, int Slowest);
}
