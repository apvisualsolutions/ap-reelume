// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Windows;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Windows;

/// <summary>
/// The one adapter that decides where anything is kept. Every path has to hang from the same root, or
/// a backup could not state what it copies and what it leaves behind.
/// <para>
/// Nothing here prints the resolved location: the assertions are about shape, and a test log is not a
/// place for somebody's profile folder.
/// </para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class AppDataPathsTests
{
    [Fact]
    public void Every_path_hangs_from_the_data_root()
    {
        var paths = new AppDataPaths(Path.Combine(Path.GetTempPath(), "APSolutions", "LocalMedia"));
        var root = paths.DataRoot + Path.DirectorySeparatorChar;

        Assert.All(
            new[]
            {
                paths.DatabasePath,
                paths.SettingsPath,
                paths.BackupsDirectory,
                paths.PersonalArtworkDirectory,
                paths.RemoteCacheDirectory,
                paths.CourseThumbnailDirectory,
                paths.DiagnosticsDirectory,
            },
            path => Assert.StartsWith(root, path, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("library.db", Path.GetFileName(paths.DatabasePath));
        Assert.Equal("settings.json", Path.GetFileName(paths.SettingsPath));
        Assert.Equal("backups", Path.GetFileName(paths.BackupsDirectory));
        Assert.Equal("personal-artwork", Path.GetFileName(paths.PersonalArtworkDirectory));
        Assert.Equal("artwork", Path.GetFileName(paths.RemoteCacheDirectory));
        Assert.Equal("cache", Path.GetFileName(Path.GetDirectoryName(paths.RemoteCacheDirectory)));

        // Under the cache and not beside the personal artwork, which is the difference that decides
        // whether the backup carries it: a frame this application took for itself is regenerable
        // from the video it came out of, and a cover somebody chose is not.
        Assert.Equal("course-thumbnails", Path.GetFileName(paths.CourseThumbnailDirectory));
        Assert.Equal("cache", Path.GetFileName(Path.GetDirectoryName(paths.CourseThumbnailDirectory)));
        Assert.Equal("diagnostics", Path.GetFileName(paths.DiagnosticsDirectory));
    }

    [Fact]
    public void The_default_root_is_the_stable_internal_identity_under_local_application_data()
    {
        var paths = new AppDataPaths();

        Assert.Equal("LocalMedia", Path.GetFileName(paths.DataRoot));
        Assert.Equal("APSolutions", Path.GetFileName(Path.GetDirectoryName(paths.DataRoot)));
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            paths.DataRoot,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Reelume", paths.DataRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_blank_root_is_refused_rather_than_resolved_to_the_working_directory()
    {
        Assert.Throws<ArgumentException>(() => new AppDataPaths("  "));
        Assert.Throws<ArgumentNullException>(() => new AppDataPaths(null!));
    }

    /// <summary>
    /// The application can be told where to live.
    /// <para>
    /// Without this there is no way to run it against anything but the one profile folder, so a
    /// lifecycle check — install, launch, upgrade, uninstall — either runs on a clean virtual machine
    /// or destroys the data of whoever is running it. There is no clean virtual machine on this
    /// hardware, and destroying somebody's library to test the installer is not a substitution.
    /// </para>
    /// </summary>
    [Fact]
    public void The_root_can_be_named_by_the_environment_so_a_run_can_be_isolated()
    {
        var requested = Path.Combine(Path.GetTempPath(), "APSolutions.LocalMedia.Tests", "named-root");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, requested);
        try
        {
            var paths = new AppDataPaths();

            Assert.Equal(Path.GetFullPath(requested), paths.DataRoot);
            Assert.StartsWith(paths.DataRoot, paths.DatabasePath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }

    /// <summary>
    /// Whether the application hands anything to Windows itself is decided by the root it was given,
    /// exactly as the startup key is.
    /// </summary>
    /// <remarks>
    /// The person who moved their data with the variable is still the person who signs in on this
    /// machine, so their browser still opens and their file dialogs still appear. A run handed a root
    /// that is neither — a harness, the walk, a lifecycle check — gets a folder under that root to
    /// put the handover in, and a folder is what makes the handover assertable: nothing in this
    /// repository could check the address the trailer button opens while that address went to a real
    /// browser.
    /// </remarks>
    [Fact]
    public void Only_a_run_that_does_not_own_the_profile_has_somewhere_to_put_a_handover()
    {
        var isolated = new AppDataPaths(Path.Combine(Path.GetTempPath(), "reelume-isolated-run"));

        Assert.Null(new AppDataPaths().SystemHandoffDirectory);
        Assert.NotNull(isolated.SystemHandoffDirectory);
        Assert.StartsWith(
            isolated.DataRoot + Path.DirectorySeparatorChar,
            isolated.SystemHandoffDirectory,
            StringComparison.OrdinalIgnoreCase);

        var requested = Path.Combine(Path.GetTempPath(), "APSolutions.LocalMedia.Tests", "owned-root");
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, requested);
        try
        {
            Assert.Null(new AppDataPaths().SystemHandoffDirectory);
            Assert.Null(new AppDataPaths(requested).SystemHandoffDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }

    [Fact]
    public void A_blank_name_in_the_environment_falls_back_to_the_profile_folder()
    {
        var previous = Environment.GetEnvironmentVariable(AppDataPaths.DataRootVariableName);
        Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, "   ");
        try
        {
            var paths = new AppDataPaths();

            Assert.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                paths.DataRoot,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppDataPaths.DataRootVariableName, previous);
        }
    }
}
