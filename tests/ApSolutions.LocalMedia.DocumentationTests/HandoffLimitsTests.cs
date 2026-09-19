// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Diagnostics;
using System.Text;

using ApSolutions.LocalMedia.TestSupport;

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The handover fits what someone reads when a session starts, and says the same in both languages.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on 2026-09-19 (ENG-035):</b> the handover had grown to 9,134 lines and 670 KB in
/// Spanish, 7,618 lines in English and 42 headings of difference, and nothing looked at it. Whoever
/// started a session did not read it to the end, so what was lost was exactly what had to be done:
/// the useful block of the last close was 56 lines. The history was frozen in
/// <c>docs/NEXT-SESSION-HISTORY.{es,en}.md</c> and the handover is now overwritten at every close.
/// </para>
/// <para>
/// The rule lives in one place, <c>eng/check-handoff.ps1</c>, which the close runs too. These tests
/// run that script: once against the real tree, and against scenes where it must sound and where it
/// must stay silent — without the second, a script that always complained would pass; without the
/// first, one that never did.
/// </para>
/// </remarks>
public sealed class HandoffLimitsTests
{
    private const string Dated = "# Dónde retomar — 2026-09-19";
    private const string DatedEnglish = "# Where to pick up — 2026-09-19";

    [Fact]
    public void The_real_handover_is_within_its_limits_in_both_languages()
    {
        var (code, output) = Run(RepositoryLayout.Root);

        Assert.True(code == 0, "eng/check-handoff.ps1 found the handover out of its limits:" + Environment.NewLine + output);
    }

    [Fact]
    public void A_short_dated_handover_with_the_same_headings_passes()
    {
        using var scene = new HandoffScene();
        scene.Write("es", Dated, "", "## Lo que se hizo", "", "· algo");
        scene.Write("en", DatedEnglish, "", "## What was done", "", "· something");

        var (code, output) = Run(scene.Root);

        Assert.True(code == 0, output);
    }

    [Fact]
    public void A_handover_over_eighty_lines_sounds()
    {
        using var scene = new HandoffScene();
        scene.Write("es", [Dated, .. Enumerable.Repeat("· línea", 80)]);
        scene.Write("en", DatedEnglish, "· line");

        var (code, output) = Run(scene.Root);

        Assert.Equal(1, code);
        Assert.Contains("NEXT-SESSION.es.md tiene 81 lineas", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_handover_over_six_kilobytes_sounds_even_when_short()
    {
        using var scene = new HandoffScene();
        scene.Write("es", Dated, new string('x', 6200));
        scene.Write("en", DatedEnglish, "· line");

        var (code, output) = Run(scene.Root);

        Assert.Equal(1, code);
        Assert.Contains("NEXT-SESSION.es.md pesa", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_section_added_in_one_language_only_sounds()
    {
        using var scene = new HandoffScene();
        scene.Write("es", Dated, "## Lo que se hizo", "## Lo que espera al propietario");
        scene.Write("en", DatedEnglish, "## What was done");

        var (code, output) = Run(scene.Root);

        Assert.Equal(1, code);
        Assert.Contains("misma estructura de encabezados", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_handover_without_its_date_on_the_first_line_sounds()
    {
        using var scene = new HandoffScene();
        scene.Write("es", "# Dónde retomar", "2026-09-19");
        scene.Write("en", DatedEnglish);

        var (code, output) = Run(scene.Root);

        Assert.Equal(1, code);
        Assert.Contains("no lleva la fecha", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_language_is_not_measured_rather_than_clean()
    {
        using var scene = new HandoffScene();
        scene.Write("es", Dated);

        var (code, output) = Run(scene.Root);

        Assert.True(code == 2, "A handover with one language missing must not read as clean:" + Environment.NewLine + output);
    }

    private static (int Code, string Output) Run(string root)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(RepositoryLayout.PathFromRoot("eng/check-handoff.ps1"));
        start.ArgumentList.Add("-Root");
        start.ArgumentList.Add(root);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("pwsh did not start, so nothing was measured.");
        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        Assert.True(process.WaitForExit(60_000), "eng/check-handoff.ps1 did not finish in a minute.");

        // The floor: a script that never reached its measurement proves nothing either way.
        var text = output.ToString();
        Assert.True(
            text.Contains("RELEVO:", StringComparison.Ordinal) || text.Contains("UNMEASURED:", StringComparison.Ordinal),
            "eng/check-handoff.ps1 never reported, so the scene measured nothing:" + Environment.NewLine + text);
        return (process.ExitCode, text);
    }

    private sealed class HandoffScene : IDisposable
    {
        public HandoffScene()
        {
            Root = Path.Combine(Path.GetTempPath(), "handoff-" + Guid.NewGuid().ToString("n")[..12]);
            Directory.CreateDirectory(Path.Combine(Root, "docs"));
        }

        public string Root { get; }

        public void Write(string language, params string[] lines) =>
            File.WriteAllText(
                Path.Combine(Root, "docs", $"NEXT-SESSION.{language}.md"),
                string.Join('\n', lines) + "\n",
                new UTF8Encoding(false));

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A scene left in the temporary folder is harmless; failing the test over it is not.
            }
        }
    }
}
