// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text;
using System.Text.Json;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// How badly a defect hurts the person using the application. The MVP gate accepts neither
/// <see cref="Critical"/> nor <see cref="Major"/>; a severity is never lowered to make a run pass.
/// </summary>
public enum DefectSeverity
{
    /// <summary>Friction, but the step still completes and nothing is lost.</summary>
    Minor,

    /// <summary>The step forces a detour, or focus, state, or context is lost on the way.</summary>
    Major,

    /// <summary>The step cannot be completed without a mouse or without a screen reader.</summary>
    Critical,
}

/// <summary>One finding, recorded with everything a second person needs to reproduce it.</summary>
public sealed record AccessibilityDefect(
    string Step,
    string Surface,
    string Control,
    DefectSeverity Severity,
    string Check,
    string Repro);

/// <summary>
/// Collects findings for one audit suite and writes them where the report can read them.
/// <para>
/// Two modes exist. The default mode is the gate: any critical or major finding fails the test, so
/// the RED cycle fails for the behaviour that is missing. <c>Audit</c> mode only records, so the
/// first pass can inventory every defect at once instead of stopping at the first one.
/// </para>
/// </summary>
public sealed class AuditLog
{
    private const string ModeVariable = "APSOLUTIONS_LOCALMEDIA_A11Y_MODE";
    private const string ResultsVariable = "APSOLUTIONS_LOCALMEDIA_A11Y_RESULTS";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly List<AccessibilityDefect> _defects = [];
    private readonly string _suite;

    public AuditLog(string suite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suite);
        _suite = suite;
    }

    /// <summary>True when the run only inventories findings instead of gating on them.</summary>
    public static bool IsAuditMode =>
        string.Equals(
            Environment.GetEnvironmentVariable(ModeVariable),
            "Audit",
            StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<AccessibilityDefect> Defects => _defects;

    /// <summary>Finds the repository root from the test binary, without hard-coding any path.</summary>
    public static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    public void Add(
        string step,
        string surface,
        string control,
        DefectSeverity severity,
        string check,
        string repro) =>
        _defects.Add(new AccessibilityDefect(step, surface, control, severity, check, repro));

    /// <summary>
    /// Writes the findings and then enforces the gate. Recording happens first on purpose: a failing
    /// assertion must still leave the evidence behind.
    /// </summary>
    public void Complete()
    {
        Save();
        if (IsAuditMode)
        {
            return;
        }

        var blocking = _defects
            .Where(defect => defect.Severity is DefectSeverity.Critical or DefectSeverity.Major)
            .ToArray();
        Assert.True(
            blocking.Length == 0,
            $"{_suite} found {blocking.Length} blocking accessibility defects:{Environment.NewLine}"
                + string.Join(
                    Environment.NewLine,
                    blocking.Select(defect =>
                        $"  [{defect.Severity}] {defect.Step}/{defect.Surface}/{defect.Control}: "
                            + $"{defect.Check} — {defect.Repro}")));
    }

    private void Save()
    {
        var directory = Environment.GetEnvironmentVariable(ResultsVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(GetRepositoryRoot(), "artifacts", "accessibility", "local");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, $"{_suite}.json"),
            JsonSerializer.Serialize(_defects, SerializerOptions));

        var report = new StringBuilder();
        report.Append("# ").Append(_suite).AppendLine();
        report.AppendLine();
        if (_defects.Count == 0)
        {
            report.AppendLine("No defects.");
        }
        else
        {
            report.AppendLine("| Severity | Step | Surface | Control | Check | Repro |");
            report.AppendLine("|---|---|---|---|---|---|");
            foreach (var defect in _defects.OrderByDescending(defect => defect.Severity))
            {
                report
                    .Append("| ").Append(defect.Severity)
                    .Append(" | ").Append(defect.Step)
                    .Append(" | ").Append(defect.Surface)
                    .Append(" | ").Append(defect.Control)
                    .Append(" | ").Append(defect.Check)
                    .Append(" | ").Append(defect.Repro)
                    .AppendLine(" |");
            }
        }

        File.WriteAllText(Path.Combine(directory, $"{_suite}.md"), report.ToString());
    }
}
