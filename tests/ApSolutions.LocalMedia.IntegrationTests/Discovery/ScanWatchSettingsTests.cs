// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Infrastructure.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

/// <summary>
/// ENG-010 and ENG-044. Whether local folders are followed live, and where that answer lives.
/// </summary>
/// <remarks>
/// The switch was painted, toggled and reset long before it was stored: it was a field on a
/// ViewModel with no store behind it and no reader in front of it, so moving it changed nothing and
/// nothing said so. These are the two halves it was missing — that the answer survives the
/// application closing, and what it means when nobody has ever given one.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class ScanWatchSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        "scan-watch-settings",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// <b>The absence of an answer means yes here, and that is deliberate.</b> Everywhere a
    /// connection is involved — automatic refresh, update checks — a missing value means no, because
    /// an installation that was never asked has not consented to reaching the network. Watching a
    /// folder on this machine reaches nothing: it reads the disk the person already pointed at. The
    /// scope record promises local changes apply within seconds, so an installation nobody has
    /// configured must still see a new film appear.
    /// </summary>
    [Fact]
    public void Local_folders_are_watched_until_somebody_says_otherwise() =>
        Assert.True(Settings().WatchLocalRoots);

    [Fact]
    public void The_answer_survives_the_application_being_closed_and_opened_again()
    {
        Settings().SetWatchLocalRoots(false);

        Assert.False(Settings().WatchLocalRoots);

        Settings().SetWatchLocalRoots(true);

        Assert.True(Settings().WatchLocalRoots);
    }

    [Fact]
    public void The_setting_needs_somewhere_to_be_written() =>
        Assert.Throws<ArgumentNullException>(() => new StoredScanWatchSettings(null!));

    /// <summary>A new reader each time, so the answer comes from the file rather than from memory.</summary>
    private StoredScanWatchSettings Settings()
    {
        Directory.CreateDirectory(_directory);
        return new StoredScanWatchSettings(new JsonSettingsStore(Path.Combine(_directory, "settings.json")));
    }
}
