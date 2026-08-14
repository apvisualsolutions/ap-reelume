// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Diagnostics;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests;

/// <summary>
/// The instrumentation on the failure path of a launch: when no window arrives, the phase has to say
/// what was true before the harness killed the process.
/// </summary>
/// <remarks>
/// On 2026-08-10 the <c>first-launch</c> phase failed once on the branch and passed with the same
/// commit on main. All it left behind was
/// <c>Window shown: False; exit code -1; 16 migration(s) applied to a new database</c> — and that
/// exit code is the harness's own kill, so the record could not say whether anything was still alive
/// to paint. That is the gap this closes.
/// <para>
/// The phase cannot be made to fail on demand without becoming the thing it is testing, so the two
/// functions are taken out of the shipped script by parsing it and run here against processes whose
/// state is known. What is checked is the property that matters most: a diagnosis on a failure path
/// must never throw, because it would replace the failure it was called to explain.
/// </para>
/// </remarks>
public sealed class LaunchDiagnosisTests
{
    /// <summary>Long enough that only a wedged child could reach it.</summary>
    private static readonly TimeSpan NeverReturned = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task A_launch_with_no_window_reports_the_process_and_the_data_folder_it_left()
    {
        var workspace = Directory.CreateTempSubdirectory("apsolutions-diagnosis-");
        try
        {
            var empty = Directory.CreateDirectory(Path.Combine(workspace.FullName, "empty"));
            var withDatabase = Directory.CreateDirectory(Path.Combine(workspace.FullName, "unreadable"));
            // A file called library.db that is not one: the read has to fail the way a database
            // caught mid-write would, and be reported instead of thrown.
            await File.WriteAllTextAsync(
                Path.Combine(withDatabase.FullName, "library.db"),
                "this is not a database",
                TestContext.Current.CancellationToken);

            var run = await RunDiagnosisAsync(workspace.FullName, empty.FullName, withDatabase.FullName);

            Assert.True(run.Exited, "The diagnosis script never returned.");
            Assert.True(run.ExitCode == 0, $"The diagnosis script failed: {run.Output}");

            var alive = Line(run.Output, "ALIVE: ");
            Assert.Contains("the process was still running", alive, StringComparison.Ordinal);
            Assert.Contains("s of processor time across", alive, StringComparison.Ordinal);
            Assert.Contains("no library.db", alive, StringComparison.Ordinal);
            Assert.Contains("the data folder is empty", alive, StringComparison.Ordinal);

            var exited = Line(run.Output, "EXITED: ");
            Assert.Contains("the process had already exited with code 7", exited, StringComparison.Ordinal);
            Assert.Contains("schema_history unread", exited, StringComparison.Ordinal);
            Assert.Contains("the data folder holds library.db", exited, StringComparison.Ordinal);
            Assert.Contains("library.db could not be read", exited, StringComparison.Ordinal);

            // The half that matters on CI: the phase line a failed launch leaves behind is the only
            // thing anyone reads, so the diagnosis has to arrive inside it.
            var phase = Line(run.Output, "PHASE: ");
            Assert.Contains("No window inside 90000 ms", phase, StringComparison.Ordinal);
            Assert.Contains("the process was still running", phase, StringComparison.Ordinal);
        }
        finally
        {
            workspace.Delete(recursive: true);
        }
    }

    private static string Line(string output, string prefix)
    {
        var line = output
            .Split('\n')
            .Select(candidate => candidate.Trim())
            .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        Assert.True(line is not null, $"The diagnosis script printed no '{prefix}' line. It said: {output}");
        return line!;
    }

    /// <summary>
    /// Runs the shipped functions, taken out of <c>eng/verify-package.ps1</c> by parsing it rather
    /// than by copying them, so a rename is a failure here instead of a silent divergence.
    /// </summary>
    private static async Task<(bool Exited, int ExitCode, string Output)> RunDiagnosisAsync(
        string workspace,
        string emptyRoot,
        string databaseRoot)
    {
        var repositoryRoot = RepositoryLayout.Root;
        var layoutRoot = Path.Combine(PackageEvidence.PackageRoot(), "layout");
        Assert.True(
            Directory.Exists(layoutRoot),
            $"The package layout is not at {layoutRoot}, so the diagnosis cannot load the SQLite the "
                + $"artifact carries. {PackageEvidence.HowToProduce}");

        var script = Path.Combine(workspace, "run-diagnosis.ps1");
        await File.WriteAllTextAsync(script, DiagnosisScript, TestContext.Current.CancellationToken);

        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in new[]
        {
            "-NoProfile",
            "-File",
            script,
            "-Source",
            Path.Combine(repositoryRoot, "eng", "verify-package.ps1"),
            "-LayoutRoot",
            layoutRoot,
            "-EmptyRoot",
            emptyRoot,
            "-DatabaseRoot",
            databaseRoot,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

        using var deadline = new CancellationTokenSource(NeverReturned);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (false, 0, await output + await error);
        }

        return (true, process.ExitCode, await output + await error);
    }

    private const string DiagnosisScript = """
        [CmdletBinding()]
        param(
            [Parameter(Mandatory)][string]$Source,
            [Parameter(Mandatory)][string]$LayoutRoot,
            [Parameter(Mandatory)][string]$EmptyRoot,
            [Parameter(Mandatory)][string]$DatabaseRoot
        )

        $ErrorActionPreference = 'Stop'

        # Open-LibraryDatabase loads SQLite from here, so the row count is attempted with the very
        # binaries the artifact carries rather than with whatever this test host happens to have.
        $layoutRoot = $LayoutRoot

        $wanted = @(
            'Open-LibraryDatabase',
            'Invoke-Scalar',
            'Get-TableCounts',
            'Get-LaunchDiagnosis',
            'Format-LaunchDiagnosis',
            'Format-WindowWait')
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($Source, [ref]$null, [ref]$null)
        $defined = @{}
        foreach ($function in $ast.FindAll(
                { param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] },
                $true)) {
            if ($wanted -contains $function.Name) {
                $defined[$function.Name] = $true
                . ([scriptblock]::Create($function.Extent.Text))
            }
        }

        $missing = @($wanted | Where-Object { -not $defined.ContainsKey($_) })
        if ($missing.Count -gt 0) {
            throw "eng/verify-package.ps1 no longer defines: $($missing -join ', ')."
        }

        $alive = Get-Process -Id $PID
        $running = Get-LaunchDiagnosis -Process $alive -DataRoot $EmptyRoot
        Write-Output "ALIVE: $(Format-LaunchDiagnosis $running)"

        # What a phase would print: a run that reached its deadline with no window to show.
        $timedOut = [pscustomobject]@{
            windowMilliseconds        = $null
            windowTimeoutMilliseconds = 90000
            launchDiagnosis           = $running
        }
        Write-Output "PHASE: $(Format-WindowWait $timedOut)"

        $exited = Start-Process -FilePath $alive.Path `
            -ArgumentList @('-NoProfile', '-Command', 'exit 7') -PassThru -WindowStyle Hidden
        $exited.WaitForExit()
        Write-Output "EXITED: $(Format-LaunchDiagnosis (Get-LaunchDiagnosis -Process $exited -DataRoot $DatabaseRoot))"
        """;
}
