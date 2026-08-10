// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

public sealed class FileReconciliationTests
{
    [Fact]
    public void Identity_slice_owns_stable_identity_provider_policy_and_decisions()
    {
        var assembly = Assembly.Load("ApSolutions.LocalMedia.Domain");

        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Domain.Discovery.FileIdentity",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Domain.Discovery.IFileIdentityProvider",
            throwOnError: false));
        Assert.NotNull(assembly.GetType(
            "ApSolutions.LocalMedia.Domain.Discovery.FileReconciliationPolicy",
            throwOnError: false));
        var decisions = assembly.GetType(
            "ApSolutions.LocalMedia.Domain.Discovery.ReconciliationDecision",
            throwOnError: false);
        Assert.NotNull(decisions);
        Assert.Equal(["Exact", "Probable", "New", "Missing"], Enum.GetNames(decisions));
    }

    [Fact]
    public void Matching_volume_and_file_id_is_exact_and_preserves_the_entity()
    {
        var policy = new FileReconciliationPolicy();
        var previous = new FileIdentity("A1", "42", "old");
        var current = new FileIdentity("A1", "42", "new");

        var assessment = policy.Assess(previous, current, matchingFingerprintCandidates: 0, sourceAvailable: true);

        Assert.Equal(ReconciliationDecision.Exact, assessment.Decision);
        Assert.False(assessment.RequiresConfirmation);
        Assert.True(assessment.PreservesEntityIdentity);
    }

    [Fact]
    public void Unique_lightweight_fingerprint_without_stable_ids_is_exact()
    {
        var policy = new FileReconciliationPolicy();
        var previous = new FileIdentity(null, null, "sha256:v1:ABC");
        var current = new FileIdentity(null, null, "sha256:v1:ABC");

        var assessment = policy.Assess(previous, current, matchingFingerprintCandidates: 1, sourceAvailable: true);

        Assert.Equal(ReconciliationDecision.Exact, assessment.Decision);
        Assert.False(assessment.RequiresConfirmation);
        Assert.True(assessment.PreservesEntityIdentity);
    }

    [Fact]
    public void Fingerprint_collision_is_probable_and_never_auto_merges()
    {
        var policy = new FileReconciliationPolicy();
        var previous = new FileIdentity(null, null, "sha256:v1:COLLISION");
        var current = new FileIdentity(null, null, "sha256:v1:COLLISION");

        var assessment = policy.Assess(previous, current, matchingFingerprintCandidates: 2, sourceAvailable: true);

        Assert.Equal(ReconciliationDecision.Probable, assessment.Decision);
        Assert.True(assessment.RequiresConfirmation);
        Assert.True(assessment.PreservesEntityIdentity);
    }

    [Fact]
    public void No_candidate_is_new_and_an_unavailable_source_is_missing()
    {
        var policy = new FileReconciliationPolicy();

        var added = policy.Assess(
            previous: null,
            new FileIdentity(null, null, "sha256:v1:NEW"),
            matchingFingerprintCandidates: 0,
            sourceAvailable: true);
        var missing = policy.Assess(
            new FileIdentity("A1", "42", null),
            current: null,
            matchingFingerprintCandidates: 0,
            sourceAvailable: false);

        Assert.Equal(ReconciliationDecision.New, added.Decision);
        Assert.False(added.PreservesEntityIdentity);
        Assert.Equal(ReconciliationDecision.Missing, missing.Decision);
        Assert.True(missing.PreservesEntityIdentity);
    }

    [Theory]
    [InlineData(null, "sha256:v1:CURRENT")]
    [InlineData("sha256:v1:PREVIOUS", null)]
    [InlineData("sha256:v1:PREVIOUS", "sha256:v1:CURRENT")]
    public void Non_matching_or_incomplete_fingerprints_are_new(
        string? previousFingerprint,
        string? currentFingerprint)
    {
        var policy = new FileReconciliationPolicy();
        var previous = new FileIdentity(null, null, previousFingerprint);
        var current = new FileIdentity(null, null, currentFingerprint);

        var assessment = policy.Assess(previous, current, matchingFingerprintCandidates: 0, sourceAvailable: true);

        Assert.Equal(ReconciliationDecision.New, assessment.Decision);
        Assert.False(assessment.PreservesEntityIdentity);
    }

    [Fact]
    public void Negative_fingerprint_candidate_count_is_rejected()
    {
        var policy = new FileReconciliationPolicy();

        Assert.Throws<ArgumentOutOfRangeException>(() => policy.Assess(
            previous: null,
            new FileIdentity(null, null, "sha256:v1:CURRENT"),
            matchingFingerprintCandidates: -1,
            sourceAvailable: true));
    }
}
