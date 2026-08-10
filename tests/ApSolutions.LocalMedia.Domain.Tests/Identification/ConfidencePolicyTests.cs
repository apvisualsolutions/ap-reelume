// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Identification;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Identification;

public sealed class ConfidencePolicyTests
{
    [Theory]
    [InlineData(0.5999, ReviewState.Pending)]
    [InlineData(0.60, ReviewState.Suggested)]
    [InlineData(0.8999, ReviewState.Suggested)]
    [InlineData(0.90, ReviewState.Automatic)]
    [InlineData(1.00, ReviewState.Automatic)]
    public void Exact_confidence_boundaries_are_stable(double score, ReviewState expected)
    {
        Assert.Equal(expected, ConfidencePolicy.Classify(score));
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(1.0001)]
    [InlineData(double.NaN)]
    public void Invalid_confidence_is_rejected(double score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfidencePolicy.Classify(score));
    }
}
