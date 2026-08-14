// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Library;

/// <summary>
/// Duplicates as versions existed as parts and never as a behaviour (LIB-008): the grouping use
/// case had a repository, a policy, and tests, and nothing in the application ever invoked it, so
/// version groups were never created on their own — the review surface compared versions that only
/// a test could have stored.
/// </summary>
public sealed class DuplicateWiringTests
{
    [Fact]
    public void Every_scan_hands_what_it_found_to_version_grouping()
    {
        var composition = CompositionSource();

        Assert.Contains(
            "GetRequiredService<GroupScannedVersions>",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public void A_group_is_reachable_from_every_copy_and_not_only_from_one()
    {
        var composition = CompositionSource();

        // Unidentified copies each carry their own title, so a group stored under one title key
        // would be invisible from the other copies' cards; the member lookup is the fallback.
        Assert.Contains("FindByMemberAsync", composition, StringComparison.Ordinal);
    }

    private static string CompositionSource()
    {
        return CompositionSourceText.Read();
    }
}
