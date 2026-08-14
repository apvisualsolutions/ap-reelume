// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.TestSupport;
using ApSolutions.LocalMedia.Windows.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.PackagingTests.Playback;

/// <summary>
/// Reads the real display configuration of the machine running the suite and records it, so the HDR
/// rows of the evidence matrix state what this hardware actually reported.
/// </summary>
public sealed class WindowsDisplayCapabilityTests
{
    [Fact]
    public async Task The_provider_reports_the_display_state_and_records_it_for_the_matrix()
    {
        var provider = new WindowsDisplayCapabilityProvider();

        var capabilities = provider.GetCurrentDisplay();

        // Enabled implies supported; the opposite would mean the query was misread.
        Assert.False(capabilities.HdrEnabled && !capabilities.SupportsHdr10);

        var report = Path.Combine(
            RepositoryLayout.Root,
            "artifacts",
            "test-results",
            "T22",
            "green",
            "display-capabilities.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        await File.WriteAllLinesAsync(
            report,
            [
                "supportsHdr10,hdrEnabled,diagnostic",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{capabilities.SupportsHdr10},{capabilities.HdrEnabled},{provider.LastDiagnostic}"),
            ],
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Repeated_queries_agree_with_each_other()
    {
        var provider = new WindowsDisplayCapabilityProvider();

        var first = provider.GetCurrentDisplay();
        var second = provider.GetCurrentDisplay();

        Assert.Equal(first, second);
    }
}
