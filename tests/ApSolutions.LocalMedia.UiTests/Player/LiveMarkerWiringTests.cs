// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The session's markers were a snapshot taken at open (BUG-008): saving, deleting, accepting, or
/// correcting a marker changed the stores and nothing recomposed what the skip button follows, so
/// a marker made during playback only worked after closing and reopening the episode.
/// </summary>
public sealed class LiveMarkerWiringTests
{
    [Fact]
    public void The_session_markers_can_be_recomposed_while_the_session_plays()
    {
        var composition = CompositionSource();

        Assert.Contains("RefreshSessionMarkersAsync", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_marker_mutation_recomposes_what_the_skip_button_follows()
    {
        var composition = CompositionSource();

        // One definition and five mutation sites: manual save, manual delete, detected accept,
        // detected correct, detected delete. Fewer means one of the paths still shows stale marks.
        var occurrences = Regex.Count(
            composition,
            @"RefreshSessionMarkersAsync\(\)");
        Assert.True(
            occurrences >= 6,
            $"RefreshSessionMarkersAsync appears {occurrences} times; the five mutation paths "
            + "and the definition make six.");
    }

    private static string CompositionSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the host keeps it.");
        return File.ReadAllText(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
