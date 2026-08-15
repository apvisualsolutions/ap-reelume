// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Infrastructure.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

/// <summary>
/// LIB-016. Whether the application may refresh stored metadata nobody asked it to, and where that
/// answer lives.
/// </summary>
/// <remarks>
/// The absence of the setting means no, exactly as it does for automatic update checks. An
/// installation that has never been asked has not answered, and a connection is not something to
/// open on the strength of a missing value.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class AutoRefreshSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        "auto-refresh-settings",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void The_automatic_refresh_is_off_until_somebody_turns_it_on() =>
        Assert.False(Settings().AutomaticRefreshEnabled);

    [Fact]
    public void The_answer_survives_the_application_being_closed_and_opened_again()
    {
        Settings().SetAutomaticRefreshEnabled(true);

        Assert.True(Settings().AutomaticRefreshEnabled);

        Settings().SetAutomaticRefreshEnabled(false);

        Assert.False(Settings().AutomaticRefreshEnabled);
    }

    [Fact]
    public void The_setting_needs_somewhere_to_be_written() =>
        Assert.Throws<ArgumentNullException>(() => new StoredAutoRefreshSettings(null!));

    /// <summary>A new reader each time, so the answer comes from the file rather than from memory.</summary>
    private StoredAutoRefreshSettings Settings()
    {
        Directory.CreateDirectory(_directory);
        return new StoredAutoRefreshSettings(new JsonSettingsStore(Path.Combine(_directory, "settings.json")));
    }
}
