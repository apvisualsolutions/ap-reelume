// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The live version switch existed as a use case and a dialog and never as an action (VSW-A01):
/// nothing in the player offered the other versions, so <c>SwitchMediaVersion</c> was registered
/// and invoked by nobody, and the dialog's buttons were wired to nothing.
/// </summary>
public sealed class VersionSwitchWiringTests
{
    [Fact]
    public void The_player_offers_the_other_versions_through_the_use_case()
    {
        var composition = CompositionSource();

        Assert.Contains("GetRequiredService<SwitchMediaVersion>", composition, StringComparison.Ordinal);
        Assert.Contains("PlayerVersionsViewModel", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_dialog_is_built_with_its_handler_instead_of_resolved_bare()
    {
        var composition = CompositionSource();

        Assert.Contains("new VersionSwitchViewModel(", composition, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AddTransient<VersionSwitchViewModel>",
            composition,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_row_hands_its_own_version_to_the_switch()
    {
        MediaVersion? handed = null;
        var version = Version(available: true);
        var row = new PlayerVersionRowViewModel(version, Question(), target =>
        {
            handed = target;
            return Task.CompletedTask;
        });

        row.SwitchCommand.Execute(null);
        for (var attempt = 0; attempt < 100 && handed is null; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Same(version, handed);
    }

    [Fact]
    public void An_unavailable_version_cannot_be_switched_to()
    {
        var row = new PlayerVersionRowViewModel(
            Version(available: false),
            Question(),
            _ => Task.CompletedTask);

        Assert.False(row.SwitchCommand.CanExecute(null));
    }

    [Fact]
    public void The_surface_only_exists_when_there_is_somewhere_to_go()
    {
        Assert.False(new PlayerVersionsViewModel([]).HasAlternatives);
        Assert.True(new PlayerVersionsViewModel([
            new PlayerVersionRowViewModel(Version(available: true), Question(), _ => Task.CompletedTask),
        ]).HasAlternatives);
    }

    private static MediaVersion Version(bool available) => new(
        new MediaFileId(Guid.NewGuid()),
        @"R:\media\film-4k.mkv",
        available,
        TimeSpan.FromMinutes(100),
        3840,
        2160,
        IsHdr: true,
        "HEVC",
        4_000_000_000);

    private static VersionSwitchViewModel Question() => new();

    private static string CompositionSource()
    {
        return CompositionSourceText.Read();
    }
}
