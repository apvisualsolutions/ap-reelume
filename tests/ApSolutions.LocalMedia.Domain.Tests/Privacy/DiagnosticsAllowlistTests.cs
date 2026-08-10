// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Privacy;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Privacy;

/// <summary>
/// What a diagnostic report is allowed to say, and what it must never say. The list is closed on
/// purpose: anything not named here does not travel, so a new field cannot leak by being forgotten.
/// </summary>
public sealed class DiagnosticsAllowlistTests
{
    [Fact]
    public void The_allowlist_names_exactly_the_fields_a_report_may_carry()
    {
        Assert.Equal(
            [
                DiagnosticsField.AppVersion,
                DiagnosticsField.WindowsVersion,
                DiagnosticsField.RuntimeVersion,
                DiagnosticsField.Locale,
                DiagnosticsField.Capabilities,
                DiagnosticsField.ErrorCode,
                DiagnosticsField.ErrorType,
                DiagnosticsField.CountBucket,
            ],
            DiagnosticsAllowlist.Allowed.Order());
    }

    [Theory]
    [InlineData("appVersion", true)]
    [InlineData("windowsVersion", true)]
    [InlineData("errorCode", true)]
    [InlineData("path", false)]
    [InlineData("fileName", false)]
    [InlineData("title", false)]
    [InlineData("contentId", false)]
    [InlineData("providerId", false)]
    [InlineData("token", false)]
    [InlineData("history", false)]
    [InlineData("libraryItems", false)]
    [InlineData("userName", false)]
    [InlineData("machineName", false)]
    [InlineData("searchTerm", false)]
    public void A_field_is_carried_only_when_the_allowlist_names_it(string field, bool allowed) =>
        Assert.Equal(allowed, DiagnosticsAllowlist.IsAllowed(field));

    [Fact]
    public void An_exception_becomes_its_type_and_nothing_else()
    {
        var sanitized = DiagnosticsAllowlist.Sanitize(new FileNotFoundException(
            "Could not find file 'D:\\media\\series\\episode.mkv'.",
            "D:\\media\\series\\episode.mkv"));

        Assert.Equal("System.IO.FileNotFoundException", sanitized);
        Assert.DoesNotContain("media", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("episode", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_nested_exception_reports_the_chain_of_types_and_no_message()
    {
        var sanitized = DiagnosticsAllowlist.Sanitize(new InvalidOperationException(
            "Scanning D:\\media failed",
            new UnauthorizedAccessException("Access to 'D:\\media\\private' is denied.")));

        Assert.Equal(
            "System.InvalidOperationException -> System.UnauthorizedAccessException",
            sanitized);
        Assert.DoesNotContain("D:", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void A_deeply_nested_exception_reports_a_bounded_chain()
    {
        Exception nested = new IOException("innermost");
        for (var depth = 0; depth < 8; depth++)
        {
            nested = new InvalidOperationException($"layer {depth}", nested);
        }

        var sanitized = DiagnosticsAllowlist.Sanitize(nested);

        Assert.Equal(4, sanitized.Split(" -> ").Length);
        Assert.DoesNotContain("layer", sanitized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Could not find 'D:\\media\\a b\\film (2019).mkv'", "path")]
    [InlineData("Access to \\\\nas\\share\\video.mkv is denied", "path")]
    [InlineData("GET https://api.themoviedb.org/3/search/movie?query=secret failed", "uri")]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.body.signature rejected", "token")]
    [InlineData("user someone on SOME-MACHINE", "identity")]
    public void A_message_that_carries_something_private_is_reduced_rather_than_trimmed(
        string message,
        string category)
    {
        var sanitized = DiagnosticsAllowlist.SanitizeMessage(message);

        Assert.Equal(DiagnosticsAllowlist.RedactedMessage, sanitized);
        Assert.DoesNotContain(category, "unused", StringComparison.Ordinal);
    }

    [Fact]
    public void A_message_with_nothing_private_in_it_survives_intact()
    {
        Assert.Equal(
            "The media engine reported an unsupported codec.",
            DiagnosticsAllowlist.SanitizeMessage("The media engine reported an unsupported codec."));
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(4, "2-5")]
    [InlineData(5, "2-5")]
    [InlineData(6, "6-20")]
    [InlineData(20, "6-20")]
    [InlineData(21, "21-100")]
    [InlineData(100, "21-100")]
    [InlineData(101, "100+")]
    [InlineData(10_000, "100+")]
    public void A_count_travels_as_a_bucket_so_a_library_cannot_be_measured(int value, string bucket) =>
        Assert.Equal(bucket, DiagnosticsAllowlist.Bucket(value));

    [Fact]
    public void A_negative_count_is_refused_rather_than_bucketed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DiagnosticsAllowlist.Bucket(-1));
    }

    [Fact]
    public void The_prohibited_list_covers_every_category_the_specification_names()
    {
        Assert.Equal(
            ["contentid", "filename", "history", "library", "machine", "path", "providerid", "title", "token", "user"],
            DiagnosticsAllowlist.ProhibitedNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Blank_input_is_refused_rather_than_treated_as_allowed()
    {
        Assert.False(DiagnosticsAllowlist.IsAllowed("   "));
        Assert.Throws<ArgumentNullException>(() => DiagnosticsAllowlist.Sanitize(null!));
        Assert.Equal(string.Empty, DiagnosticsAllowlist.SanitizeMessage(string.Empty));
    }
}
