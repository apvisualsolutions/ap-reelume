// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.DocumentationTests;

/// <summary>
/// The MVP gate, expressed as a suite rather than as a reading of a table.
/// </summary>
/// <remarks>
/// The rule this enforces is not "everything is verified" — that would be a rule the machine can
/// satisfy by being told so. It is that <b>nothing is informally pending</b>: every MVP commitment
/// carries an explicit status, every settled one carries the evidence that settled it, and every
/// unsettled one names what is blocking it and what would unblock it. A commitment that is neither
/// verified nor blocked is the one that gets shipped by accident.
/// </remarks>
public sealed class FeatureCoverageTests
{
    private const int MvpCommitments = 46;

    [Fact]
    public void The_matrix_carries_exactly_the_commitments_the_release_was_scoped_around()
    {
        // 54 since 2026-08-09: PLY-016 (low-resolution quality enhancement) entered the scope by
        // the owner's request, POST_STABLE and PLANNED, with its decisions taken in its plan.
        // 55 since 2026-08-23: PRD-006, the visual-parity undertaking itself, STABLE and
        // IN_PROGRESS until the captures matrix lands - the redesign was scope without a row,
        // which is exactly what this gate exists to refuse.
        // 60 since 2026-08-30: CRS-001..005, courses, POST_STABLE and DESIGN_APPROVED once
        // ADR-0006 was accepted. The design package had them prototyped and specified while the
        // matrix said nothing, and scope the matrix does not carry is scope nobody decided.
        // 62 since 2026-09-03, by the owner's decision: CRS-006, a course card's picture taken from
        // the video itself, and LIB-018, setting your own cover on anything that has one. The second
        // arrived because measuring the first found the store, the backup and the view model all
        // ready for a personal cover and nothing anywhere that could choose one.
        // 66 later the same day: the rail menu the prototype never draws was measured and rejected
        // as UX-009, and the three gaps it had been hiding entered on their own - LIB-019 (rescanning
        // a root on request, whose manual trigger the engine already declares with no caller in
        // src/), LIB-020 (filtering the review inbox and the duplicate groups) and CRS-007 (filtering
        // the courses grid, whose three strings were already translated and consumed by nobody).
        // 71 the same day, and NOT because scope grew again: this is the widening the line above was
        // waiting for. LIB-013..017 were always in the matrix; the reader named the three releases it
        // knew and those five rows carried a fourth, so the number this asserted was the number it
        // could read. Both counts are now taken from the same 71 rows, which is why they agree — and
        // agreeing is the point. They disagreed all day precisely because five rows were invisible to
        // one of them. This number and verify-docs.ps1's are measured after the change, never written
        // ahead of it.
        Assert.Equal(71, FeatureMatrix.Rows.Count);
        Assert.Equal(MvpCommitments, FeatureMatrix.Mvp.Count);
        Assert.Equal(
            FeatureMatrix.Rows.Count,
            FeatureMatrix.Rows.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// The manifest and the matrix are two views of one decision. If they can disagree, one of them
    /// is decoration.
    /// </summary>
    [Fact]
    public void The_manifest_covers_every_mvp_commitment_and_invents_none()
    {
        var inMatrix = FeatureMatrix.Mvp.Select(row => row.Id).Order(StringComparer.Ordinal).ToArray();
        var inManifest = FeatureMatrix.ManifestFeatures()
            .Select(feature => feature.GetProperty("id").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(inMatrix, inManifest);
    }

    [Fact]
    public void The_manifest_reports_the_same_status_the_matrix_does()
    {
        var matrix = FeatureMatrix.Mvp.ToDictionary(row => row.Id, row => row.Status, StringComparer.Ordinal);

        var disagreements = FeatureMatrix.ManifestFeatures()
            .Select(feature => (
                Id: feature.GetProperty("id").GetString()!,
                Manifest: feature.GetProperty("status").GetString()!))
            .Where(entry => matrix[entry.Id] != entry.Manifest)
            .Select(entry => $"{entry.Id}: matrix says {matrix[entry.Id]}, manifest says {entry.Manifest}")
            .ToArray();

        Assert.True(disagreements.Length == 0, string.Join("; ", disagreements));
    }

    /// <summary>
    /// The matrix has to be readable as a whole before any count taken from it means anything.
    /// </summary>
    /// <remarks>
    /// This is the gate that was missing on 2026-09-03. Five rows used a release the parser did not
    /// name, so they were not rejected — they were skipped, and every question asked of the matrix
    /// was answered over the 60 rows that happened to match. A parser that drops what it does not
    /// recognise gives a short answer with a confident face, which is worse than no answer.
    /// </remarks>
    [Fact]
    public void Every_row_of_the_matrix_parses_and_uses_only_what_the_matrix_declares()
    {
        Assert.True(FeatureMatrix.Problems.Count == 0, string.Join("; ", FeatureMatrix.Problems));
        Assert.NotEmpty(FeatureMatrix.DeclaredTargets);
        Assert.NotEmpty(FeatureMatrix.DeclaredStatuses);
    }

    /// <summary>
    /// A status of `VERIFIED` with nothing linked is an assertion, not a verification.
    /// </summary>
    /// <remarks>
    /// Read over every row rather than only the MVP ones since 2026-09-03. While the parser could
    /// not see LIB-013..017, this asked its question of a set that excluded them, and LIB-017 sat
    /// `VERIFIED` with nothing at all checking that its evidence existed or was even linked. The
    /// rule was never "an MVP claim needs evidence"; it is that a claim needs evidence.
    /// </remarks>
    [Fact]
    public void Nothing_is_verified_without_evidence_linked_from_the_matrix()
    {
        var unsupported = FeatureMatrix.Rows
            .Where(row => row.Status == "VERIFIED" && row.EvidenceLinks.Length == 0)
            .Select(row => row.Id)
            .ToArray();

        Assert.True(unsupported.Length == 0, $"Verified with no evidence linked: {string.Join(", ", unsupported)}.");
    }

    /// <summary>
    /// The negative commitment is a commitment too: `UX-008` is out of scope, and the evidence of its
    /// absence is what keeps it from quietly becoming a feature.
    /// </summary>
    [Fact]
    public void The_excluded_commitment_stays_excluded_and_says_where_that_was_checked()
    {
        var excluded = Assert.Single(FeatureMatrix.Mvp, row => row.Status == "OUT_OF_SCOPE");

        Assert.Equal("UX-008", excluded.Id);
        Assert.NotEmpty(excluded.EvidenceLinks);
    }

    /// <summary>
    /// The gate itself. Anything not settled has to name its blocker, its owner, and the condition
    /// that would clear it — otherwise "not yet" and "nobody looked" are the same entry.
    /// </summary>
    [Fact]
    public void Every_unsettled_commitment_declares_what_is_blocking_it_and_what_would_clear_it()
    {
        var offenders = new List<string>();
        foreach (var feature in FeatureMatrix.ManifestFeatures())
        {
            var id = feature.GetProperty("id").GetString()!;
            var status = feature.GetProperty("status").GetString()!;
            if (FeatureMatrix.SettledStatuses.Contains(status))
            {
                continue;
            }

            if (!feature.TryGetProperty("blocker", out var blocker) || blocker.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                offenders.Add($"{id} is {status} with no blocker declared");
                continue;
            }

            foreach (var field in new[] { "reason", "owner", "unblockCondition" })
            {
                if (!blocker.TryGetProperty(field, out var value) || string.IsNullOrWhiteSpace(value.GetString()))
                {
                    offenders.Add($"{id}'s blocker has no {field}");
                }
            }
        }

        Assert.True(offenders.Count == 0, string.Join("; ", offenders));
    }

    /// <summary>
    /// A settled commitment must not carry a blocker: that combination is how a block quietly turns
    /// into a pass while the paperwork still says otherwise.
    /// </summary>
    [Fact]
    public void A_settled_commitment_carries_no_blocker()
    {
        var contradictions = FeatureMatrix.ManifestFeatures()
            .Where(feature => FeatureMatrix.SettledStatuses.Contains(feature.GetProperty("status").GetString()!))
            .Where(feature => feature.TryGetProperty("blocker", out var blocker)
                && blocker.ValueKind == System.Text.Json.JsonValueKind.Object)
            .Select(feature => feature.GetProperty("id").GetString()!)
            .ToArray();

        Assert.True(
            contradictions.Length == 0,
            $"Settled but still carrying a blocker: {string.Join(", ", contradictions)}.");
    }

    /// <summary>
    /// Every commitment says which tasks built it and which suites hold it up. Without that the
    /// manifest records an outcome and loses the reason for it.
    /// </summary>
    [Fact]
    public void Every_commitment_names_the_tasks_and_the_suites_behind_it()
    {
        var thin = FeatureMatrix.ManifestFeatures()
            .Where(feature => feature.GetProperty("tasks").GetArrayLength() == 0
                || feature.GetProperty("tests").GetArrayLength() == 0)
            .Select(feature => feature.GetProperty("id").GetString()!)
            .ToArray();

        Assert.True(thin.Length == 0, $"No task or no suite recorded for: {string.Join(", ", thin)}.");
    }

    /// <summary>
    /// The manifest describes one commit and one artifact. Without that pair it describes a release
    /// nobody can identify.
    /// </summary>
    [Fact]
    public void The_manifest_identifies_the_release_it_describes()
    {
        var manifest = FeatureMatrix.Manifest();

        Assert.Equal("MVP", manifest.GetProperty("release").GetString());
        Assert.Matches("^[0-9a-f]{40}$", manifest.GetProperty("commit").GetString()!);
        Assert.False(string.IsNullOrWhiteSpace(manifest.GetProperty("version").GetString()));
        Assert.NotEmpty(manifest.GetProperty("artifacts").EnumerateArray());
    }
}
