// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Metadata;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Metadata;

public sealed class MetadataRefreshPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-91, true)]
    [InlineData(-90, true)]
    [InlineData(-89, false)]
    [InlineData(0, false)]
    public void Ninety_days_is_where_an_entry_becomes_stale(int daysAgo, bool isStale) =>
        Assert.Equal(isStale, MetadataRefreshPolicy.IsStale(Now.AddDays(daysAgo), Now));

    /// <summary>
    /// An entry with no date was never refreshed, so it is the stalest there is. Measured on
    /// 2026-08-15: no production path writes an identified row without one, so this is the guard for
    /// a row nothing currently writes rather than a case in the field.
    /// </summary>
    [Fact]
    public void An_entry_with_no_date_was_never_refreshed() =>
        Assert.True(MetadataRefreshPolicy.IsStale(null, Now));

    [Fact]
    public void A_future_date_is_not_stale() =>
        Assert.False(MetadataRefreshPolicy.IsStale(Now.AddDays(1), Now));

    [Fact]
    public void The_cap_contains_the_first_pass_over_a_whole_library() =>
        Assert.Equal(20, MetadataRefreshPolicy.MaximumPerPass);

    [Fact]
    public void Stale_before_is_the_moment_an_entry_has_to_predate() =>
        Assert.Equal(Now.AddDays(-90), MetadataRefreshPolicy.StaleBefore(Now));
}
