// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

/// <summary>
/// The two halves of a file's identity, and what happens when one of them cannot be had.
/// </summary>
/// <remarks>
/// TST-001's debt. The end-to-end scans covered the path where both halves answer and nothing
/// covered the failures the class exists for — a volume with no stable ids, a file another process
/// is holding — which left it at 16% of its branches. The point of this composite is that either
/// half may fail without costing the other, and that is only true if it is exercised.
/// </remarks>
public sealed class CompositeFileIdentityTests
{
    public static TheoryData<Exception> IdentityFailures() =>
    [
        new IOException("the file is in use"),
        new UnauthorizedAccessException("no rights to it"),
        new Win32Exception(5),
        new PlatformNotSupportedException("this volume has no stable ids"),
    ];

    [Fact]
    public async Task Both_halves_answering_produces_both_halves()
    {
        var provider = new CompositeFileIdentityProvider(
            Answers(new FileIdentity("volume-1", "file-1", null)),
            Answers(new FileIdentity(null, null, "fingerprint-1")));

        var identity = await provider.GetAsync(Path, Metadata, TestContext.Current.CancellationToken);

        Assert.Equal("volume-1", identity.VolumeId);
        Assert.Equal("file-1", identity.FileId);
        Assert.Equal("fingerprint-1", identity.Fingerprint);
    }

    /// <summary>
    /// A volume that cannot give a stable id must not cost the fingerprint, which is the half that
    /// still lets reconciliation recognise the file after a move.
    /// </summary>
    [Theory]
    [MemberData(nameof(IdentityFailures))]
    public async Task A_stable_id_that_cannot_be_had_does_not_cost_the_fingerprint(Exception failure)
    {
        var provider = new CompositeFileIdentityProvider(
            Throws(failure),
            Answers(new FileIdentity(null, null, "fingerprint-1")));

        var identity = await provider.GetAsync(Path, Metadata, TestContext.Current.CancellationToken);

        Assert.Null(identity.VolumeId);
        Assert.Null(identity.FileId);
        Assert.Equal("fingerprint-1", identity.Fingerprint);
        Assert.False(identity.HasStableFileId);
    }

    [Theory]
    [MemberData(nameof(IdentityFailures))]
    public async Task A_fingerprint_that_cannot_be_read_does_not_cost_the_stable_id(Exception failure)
    {
        var provider = new CompositeFileIdentityProvider(
            Answers(new FileIdentity("volume-1", "file-1", null)),
            Throws(failure));

        var identity = await provider.GetAsync(Path, Metadata, TestContext.Current.CancellationToken);

        Assert.True(identity.HasStableFileId);
        Assert.Null(identity.Fingerprint);
    }

    /// <summary>
    /// Neither half is not an error: it is a file without identity, and reconciliation is written to
    /// treat it as one. Throwing here would fail a whole scan over one unreadable file.
    /// </summary>
    [Fact]
    public async Task A_file_that_yields_neither_half_is_a_file_without_identity()
    {
        var provider = new CompositeFileIdentityProvider(
            Throws(new IOException("locked")),
            Throws(new UnauthorizedAccessException("locked")));

        var identity = await provider.GetAsync(Path, Metadata, TestContext.Current.CancellationToken);

        Assert.Null(identity.VolumeId);
        Assert.Null(identity.FileId);
        Assert.Null(identity.Fingerprint);
    }

    /// <summary>
    /// The filter is a list of the failures that mean "not available", not a way to swallow
    /// everything. A defect has to keep travelling, or the scan goes quietly wrong instead.
    /// </summary>
    [Fact]
    public async Task A_failure_that_is_not_about_availability_is_not_swallowed()
    {
        var provider = new CompositeFileIdentityProvider(
            Throws(new InvalidOperationException("this is a defect, not a busy file")),
            Answers(new FileIdentity(null, null, "fingerprint-1")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetAsync(Path, Metadata, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_composite_missing_either_half_is_refused_at_construction()
    {
        var half = Answers(new FileIdentity(null, null, null));

        Assert.Throws<ArgumentNullException>(() => new CompositeFileIdentityProvider(null!, half));
        Assert.Throws<ArgumentNullException>(() => new CompositeFileIdentityProvider(half, null!));
    }

    private const string Path = @"R:\media\film.mkv";

    private static TechnicalMetadata Metadata => new(
        TimeSpan.FromMinutes(90),
        "mkv",
        ["HEVC"],
        ["EAC3"],
        1920,
        1080);

    private static StubProvider Answers(FileIdentity identity) => new(identity, null);

    private static StubProvider Throws(Exception failure) => new(null, failure);

    private sealed class StubProvider(FileIdentity? identity, Exception? failure) : IFileIdentityProvider
    {
        public Task<FileIdentity> GetAsync(
            string path,
            TechnicalMetadata technicalMetadata,
            CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(identity!)
                : Task.FromException<FileIdentity>(failure);
    }
}
