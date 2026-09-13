// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The engineering register keeps its order, and the order is the whole point of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule is the owner's, given on 2026-09-13</b>: the oldest task at the top, new ones appended
/// at the bottom, and you take the first one that is not <c>PARADA</c>. His reason is the one that
/// makes a gate worth writing — whoever reads the file, a person or an agent, reads top to bottom and
/// does not reach the end. With new work at the top the old work never gets done, the list grows
/// forever, and new tasks pile in on top of unfinished ones.
/// </para>
/// <para>
/// So what is asserted here is what a person forgets: that the dates and the numbers climb downwards.
/// A row appearing above one that is older is a task queued ahead of its turn, and that is the one
/// mistake this file cannot survive — it would look exactly like an ordered list while behaving like
/// an unordered one.
/// </para>
/// <para>
/// The register holds what <c>FEATURES.md</c> cannot: chores, gates, debt, defects and unmeasured
/// questions. Anything that is scope keeps its row in the matrix and is only named here, because the
/// same task in two places is how both copies drift.
/// </para>
/// </remarks>
public sealed class TareasRegisterTests
{
    private static readonly string Register =
        Path.Combine(RepositoryRoot(), "docs", "TAREAS.md");

    /// <summary>Ids are <c>ENG-</c> and three digits, and nothing else.</summary>
    private static readonly Regex Row = new(
        @"^\|\s*`(ENG-\d{3})`\s*\|\s*(\d{4}-\d{2}-\d{2})\s*\|\s*(.*?)\s*\|",
        RegexOptions.Compiled);

    private static readonly string[] Statuses =
        ["`ABIERTA`", "`EN CURSO`", "`PARADA`", "`DEL PROPIETARIO`", "`POR COMPROBAR`"];

    /// <summary>
    /// The register exists and carries rows, which is the floor every assertion below needs.
    /// </summary>
    /// <remarks>
    /// Without it, a register renamed or emptied would make every other test here pass by having
    /// nothing to disagree with — the shape of a green that means nobody looked.
    /// </remarks>
    [Fact]
    public void The_register_exists_and_carries_rows()
    {
        Assert.True(File.Exists(Register), $"{Register} is missing, so nothing below is measuring it.");

        var (open, done) = ReadRows();

        Assert.NotEmpty(open);
        Assert.NotEmpty(done);
    }

    /// <summary>
    /// The open list climbs: every row is newer than the one above it, and numbered higher.
    /// </summary>
    /// <remarks>
    /// The owner's rule, made mechanical. Dates alone would let two tasks born the same day be
    /// swapped; the identifier settles it, because identifiers are handed out in order of birth.
    /// </remarks>
    [Fact]
    public void The_open_list_puts_the_oldest_first_and_never_queues_a_newer_one_above_it()
    {
        var (open, _) = ReadRows();

        for (var index = 1; index < open.Count; index++)
        {
            var above = open[index - 1];
            var here = open[index];

            Assert.True(
                here.Born >= above.Born,
                $"{here.Id} was born {here.Born:yyyy-MM-dd} and sits below {above.Id}, born "
                    + $"{above.Born:yyyy-MM-dd}. The oldest goes first, or the old work never gets "
                    + "done: whoever reads this file reads top to bottom and does not reach the end.");
            Assert.True(
                string.CompareOrdinal(here.Id, above.Id) > 0,
                $"{here.Id} sits below {above.Id}, so a task has been queued ahead of its turn. "
                    + "Identifiers are handed out in order of birth and never reused.");
        }
    }

    /// <summary>Every row names a status the register declares, and nothing else.</summary>
    /// <remarks>
    /// A status invented in passing reads like the others and is skipped by nobody. The closed set is
    /// what makes <c>PARADA</c> mean «skip this and keep reading» rather than «somebody typed
    /// something here».
    /// </remarks>
    [Fact]
    public void Every_open_row_names_one_of_the_statuses_the_register_declares()
    {
        var (open, _) = ReadRows();

        var strange = open.Where(row => !Statuses.Contains(row.Status, StringComparer.Ordinal))
            .Select(row => $"{row.Id}: {row.Status}")
            .ToArray();

        Assert.True(
            strange.Length == 0,
            "These rows carry a status the register does not declare, so nobody knows whether to "
                + $"skip them: {string.Join(", ", strange)}");
    }

    /// <summary>No identifier is used twice, in either list.</summary>
    /// <remarks>
    /// Closing a task moves it to the bottom list <b>with its number</b>, so the gaps in the open
    /// list are what got done. Reusing a number would make two different pieces of work share a name
    /// and quietly rewrite history.
    /// </remarks>
    [Fact]
    public void No_identifier_is_used_twice_across_the_two_lists()
    {
        var (open, done) = ReadRows();

        var duplicates = open.Concat(done)
            .GroupBy(row => row.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.True(
            duplicates.Length == 0,
            $"These identifiers appear more than once: {string.Join(", ", duplicates)}. A closed task "
                + "keeps its number and a new one takes the next.");
    }

    /// <summary>A task that is stopped says what stops it.</summary>
    /// <remarks>
    /// <c>PARADA</c> tells a reader to skip the row and keep going, and a reader who cannot see the
    /// blocker has no way to tell a real one from a task somebody gave up on. So the row has to name
    /// it, and this looks for a named obstacle rather than for a word.
    /// </remarks>
    [Fact]
    public void A_stopped_task_names_what_stops_it()
    {
        var (open, _) = ReadRows();

        var silent = open
            .Where(row => row.Status == "`PARADA`")
            .Where(row => !row.Line.Contains("bloque", StringComparison.OrdinalIgnoreCase)
                && !row.Line.Contains("espera", StringComparison.OrdinalIgnoreCase)
                && !row.Line.Contains("depende", StringComparison.OrdinalIgnoreCase)
                && !row.Line.Contains("hasta", StringComparison.OrdinalIgnoreCase))
            .Select(row => row.Id)
            .ToArray();

        Assert.True(
            silent.Length == 0,
            $"These rows are PARADA without naming what stops them: {string.Join(", ", silent)}. A "
                + "blocker nobody can read is indistinguishable from a task somebody gave up on.");
    }

    /// <summary>
    /// The register does not repeat a row that the scope matrix already carries.
    /// </summary>
    /// <remarks>
    /// The whole reason this file exists beside <c>FEATURES.md</c> is that the matrix cannot hold a
    /// chore — it demands a target release, an acceptance criterion and linked evidence. The reverse
    /// has to hold too: a scope identifier appearing as a row here would be the same work in two
    /// places, which is the defect the register was created to remove. Naming one in prose is
    /// expected and is what the «lo que vive en la matriz» section is for.
    /// </remarks>
    [Fact]
    public void The_register_carries_no_row_whose_work_belongs_to_a_scope_identifier()
    {
        var lines = File.ReadAllLines(Register);
        var scope = new Regex(@"^\|\s*`(PRD|REL|LIB|UX|CRS|PLY)-\d{3}`\s*\|", RegexOptions.Compiled);

        var trespassers = lines.Where(line => scope.IsMatch(line)).ToArray();

        Assert.True(
            trespassers.Length == 0,
            "These rows give a scope identifier a row of its own, so the same work now lives in two "
                + $"places: {string.Join(" / ", trespassers)}");
    }

    private static (List<Entry> Open, List<Entry> Done) ReadRows()
    {
        var open = new List<Entry>();
        var done = new List<Entry>();
        var target = open;
        var seenOpen = false;

        foreach (var line in File.ReadAllLines(Register))
        {
            if (line.StartsWith("## Abiertas", StringComparison.Ordinal))
            {
                target = open;
                seenOpen = true;
                continue;
            }

            if (line.StartsWith("## Hechas", StringComparison.Ordinal))
            {
                target = done;
                continue;
            }

            if (!seenOpen)
            {
                continue;
            }

            var match = Row.Match(line);
            if (match.Success)
            {
                target.Add(new Entry(
                    match.Groups[1].Value,
                    DateOnly.ParseExact(match.Groups[2].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    match.Groups[3].Value,
                    line));
            }
        }

        return (open, done);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("the repository root was not found.");
    }

    /// <summary>One row of the register.</summary>
    /// <param name="Status">For a done row this is the closing date, which no status test reads.</param>
    private sealed record Entry(string Id, DateOnly Born, string Status, string Line);
}
