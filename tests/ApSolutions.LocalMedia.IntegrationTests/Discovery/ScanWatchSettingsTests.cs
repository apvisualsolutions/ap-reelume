// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Discovery;
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

    /// <summary>
    /// An installation nobody has configured sweeps at the interval the code ships with, which is
    /// the one number rather than the two that used to disagree (ENG-044).
    /// </summary>
    [Fact]
    public void An_installation_nobody_has_asked_sweeps_at_the_interval_the_code_ships_with() =>
        Assert.Equal(ScanWatchPolicy.DefaultSweepInterval, Settings().SweepInterval);

    [Fact]
    public void The_sweep_interval_survives_the_application_being_closed_and_opened_again()
    {
        Settings().SetSweepInterval(TimeSpan.FromMinutes(45));

        Assert.Equal(TimeSpan.FromMinutes(45), Settings().SweepInterval);
    }

    /// <summary>
    /// <b>The settings file is plain text and somebody can open it.</b> A sweep every zero minutes
    /// is a scan in a hot loop, and one every hundred years never arrives; both are brought back to
    /// the bounds the screen's spinner already paints. The clamping lives here, in the only place
    /// that reads a file a person can edit — the scheduler is told the value is sound and believes
    /// it, which is what lets a test hand it a hundred milliseconds instead of waiting a minute.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_sweep_shorter_than_the_floor_is_brought_up_to_it(int minutes)
    {
        Settings().SetSweepInterval(TimeSpan.FromMinutes(minutes));

        Assert.Equal(ScanWatchPolicy.MinimumSweepInterval, Settings().SweepInterval);
    }

    [Fact]
    public void A_sweep_longer_than_the_ceiling_is_brought_down_to_it()
    {
        Settings().SetSweepInterval(TimeSpan.FromDays(400));

        Assert.Equal(ScanWatchPolicy.MaximumSweepInterval, Settings().SweepInterval);
    }

    /// <summary>
    /// The watching restarts when either value moves, so both have to say so. Without this the
    /// screen would be back where ENG-044 found it: a control that only takes effect next launch.
    /// </summary>
    [Fact]
    public void Changing_either_value_tells_whoever_is_listening()
    {
        var settings = Settings();
        var announced = 0;
        settings.Changed += (_, _) => announced++;

        settings.SetWatchLocalRoots(false);
        settings.SetSweepInterval(TimeSpan.FromMinutes(20));

        Assert.Equal(2, announced);
    }

    /// <summary>Writing with nobody listening is the ordinary case and must not throw.</summary>
    [Fact]
    public void Writing_a_setting_with_nobody_listening_is_harmless()
    {
        var settings = Settings();

        settings.SetWatchLocalRoots(false);

        Assert.False(settings.WatchLocalRoots);
    }

    /// <summary>A new reader each time, so the answer comes from the file rather than from memory.</summary>
    private StoredScanWatchSettings Settings()
    {
        Directory.CreateDirectory(_directory);
        return new StoredScanWatchSettings(new JsonSettingsStore(Path.Combine(_directory, "settings.json")));
    }
}
