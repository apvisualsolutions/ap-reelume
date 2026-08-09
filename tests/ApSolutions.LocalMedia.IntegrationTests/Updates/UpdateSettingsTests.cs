using ApSolutions.LocalMedia.Infrastructure.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Updates;

/// <summary>
/// Whether the application may look for updates on its own, and where that answer lives.
/// </summary>
/// <remarks>
/// The absence of the setting means no. An installation that has never been asked has not answered,
/// and a connection is not something to open on the strength of a missing value.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class UpdateSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        "update-settings",
        Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Automatic_checks_are_off_until_somebody_turns_them_on()
    {
        Assert.False(Settings().AutomaticCheckEnabled);
    }

    [Fact]
    public void The_answer_survives_the_application_being_closed_and_opened_again()
    {
        Settings().SetAutomaticCheckEnabled(true);

        Assert.True(Settings().AutomaticCheckEnabled);

        Settings().SetAutomaticCheckEnabled(false);

        Assert.False(Settings().AutomaticCheckEnabled);
    }

    [Fact]
    public void The_setting_needs_somewhere_to_be_written()
    {
        Assert.Throws<ArgumentNullException>(() => new StoredUpdateSettings(null!));
    }

    /// <summary>A new reader each time, so the answer comes from the file rather than from memory.</summary>
    private StoredUpdateSettings Settings()
    {
        Directory.CreateDirectory(_directory);
        return new StoredUpdateSettings(new JsonSettingsStore(Path.Combine(_directory, "settings.json")));
    }
}
