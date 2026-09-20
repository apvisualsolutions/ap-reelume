// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Metadata;

/// <summary>
/// The general cover order as it survives closing the application (LIB-021, ADR-0009 decision 4).
/// </summary>
/// <remarks>
/// The store is the real one over a temporary file rather than a double, which is how the lifecycle
/// settings are measured too: what matters here is the round trip through JSON, and a dictionary
/// standing in for the file would pass while the file did not.
/// </remarks>
public sealed class StoredCoverOrderSettingsTests
{
    [Fact]
    public void A_store_that_has_never_been_asked_gives_the_default_order()
    {
        var directory = Directory.CreateTempSubdirectory("reelume-cover-order");
        try
        {
            var settings = NewSettings(directory);

            Assert.Equal(CoverOrderPolicy.Default, settings.Current);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void What_was_saved_comes_back_after_a_restart()
    {
        var directory = Directory.CreateTempSubdirectory("reelume-cover-order");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            new StoredCoverOrderSettings(new JsonSettingsStore(path))
                .Save([CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal]);

            var reopened = new StoredCoverOrderSettings(new JsonSettingsStore(path)).Current;

            Assert.Equal([CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal], reopened);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// An order saved short would leave an origin no title could ever draw, so it is completed on
    /// the way in — and the control that says the repair is not the reader's is reading it back.
    /// </summary>
    [Fact]
    public void An_order_saved_short_is_completed_rather_than_stored_as_given()
    {
        var directory = Directory.CreateTempSubdirectory("reelume-cover-order");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            new StoredCoverOrderSettings(new JsonSettingsStore(path)).Save([CoverOrigin.Frame]);

            Assert.Contains("Frame,Personal,Provider", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(
                [CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider],
                new StoredCoverOrderSettings(new JsonSettingsStore(path)).Current);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_hand_edited_file_that_names_nothing_valid_gives_the_default_order()
    {
        var directory = Directory.CreateTempSubdirectory("reelume-cover-order");
        try
        {
            var path = Path.Combine(directory.FullName, "settings.json");
            File.WriteAllText(
                path,
                """
                {
                  "covers.order": "Nonsense"
                }
                """);

            Assert.Equal(CoverOrderPolicy.Default, new StoredCoverOrderSettings(new JsonSettingsStore(path)).Current);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void A_settings_object_with_nothing_behind_it_is_refused()
    {
        var directory = Directory.CreateTempSubdirectory("reelume-cover-order");
        try
        {
            Assert.Throws<ArgumentNullException>(() => new StoredCoverOrderSettings(null!));
            Assert.Throws<ArgumentNullException>(() => NewSettings(directory).Save(null!));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static StoredCoverOrderSettings NewSettings(DirectoryInfo directory) =>
        new(new JsonSettingsStore(Path.Combine(directory.FullName, "settings.json")));
}
