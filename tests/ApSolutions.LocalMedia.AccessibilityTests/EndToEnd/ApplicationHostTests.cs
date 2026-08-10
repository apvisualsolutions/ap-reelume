// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Windows;
using ApSolutions.LocalMedia.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// The application as something that can be let go of.
/// </summary>
/// <remarks>
/// ARQ-001. The composition root kept its provider in a static field nothing ever released, so the
/// process handed LibVLC, SQLite, the tray icon and the hotkey registrations to Windows to reclaim.
/// That is not a teardown, and it had a second cost that these tests are mostly about: two
/// applications could not exist at once in one process, because the second overwrote the first's
/// services. Removing <c>DisableParallelization</c> from the assembled collection is the other half
/// of the proof.
/// </remarks>
[Collection(AssembledShellSuites.Name)]
public sealed class ApplicationHostTests : IDisposable
{
    private readonly string _dataRoot = Path.Combine(
        Path.GetTempPath(),
        "APSolutions.LocalMedia.Tests",
        $"host-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }

    /// <summary>
    /// Disposing the host disposes the container, which is what runs every singleton's own release.
    /// The container is checked rather than each service: it is the one thing that, once gone,
    /// guarantees the rest went with it.
    /// </summary>
    [Fact]
    public async Task Disposing_the_application_disposes_the_container_that_owns_its_singletons()
    {
        var host = ApplicationHost.Create(new AppDataPaths(Path.Combine(_dataRoot, "single")));
        var database = host.Services.GetRequiredService<SqliteConnectionFactory>();
        Assert.NotNull(database);

        await host.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(
            () => host.Services.GetRequiredService<SqliteConnectionFactory>());
    }

    /// <summary>
    /// Shutdown arrives from the window, from the tray, and from whoever owns the host, and none of
    /// them can know about the others.
    /// </summary>
    [Fact]
    public async Task Releasing_the_application_twice_is_not_an_error()
    {
        var host = ApplicationHost.Create(new AppDataPaths(Path.Combine(_dataRoot, "twice")));

        await host.DisposeAsync();
        await host.DisposeAsync();
    }

    /// <summary>
    /// The defect the static field caused, stated as a test: two applications in one process each
    /// keep their own services. It is why the assembled suites had to run one after another.
    /// </summary>
    [Fact]
    public async Task Two_applications_in_one_process_keep_their_own_services()
    {
        var firstRoot = Path.Combine(_dataRoot, "first");
        var secondRoot = Path.Combine(_dataRoot, "second");
        await using var first = ApplicationHost.Create(new AppDataPaths(firstRoot));
        await using var second = ApplicationHost.Create(new AppDataPaths(secondRoot));

        Assert.Equal(firstRoot, first.Services.GetRequiredService<IAppDataPaths>().DataRoot);
        Assert.Equal(secondRoot, second.Services.GetRequiredService<IAppDataPaths>().DataRoot);
        Assert.NotSame(
            first.Services.GetRequiredService<SqliteConnectionFactory>(),
            second.Services.GetRequiredService<SqliteConnectionFactory>());
    }

    /// <summary>
    /// The surfaces the container builds reach the application they belong to, and each container
    /// publishes its own.
    /// </summary>
    [Fact]
    public async Task Each_application_publishes_itself_to_the_surfaces_it_builds()
    {
        await using var first = ApplicationHost.Create(new AppDataPaths(Path.Combine(_dataRoot, "a")));
        await using var second = ApplicationHost.Create(new AppDataPaths(Path.Combine(_dataRoot, "b")));

        Assert.Same(first, first.Services.GetRequiredService<ApplicationHost.Accessor>().Required);
        Assert.Same(second, second.Services.GetRequiredService<ApplicationHost.Accessor>().Required);
    }

    /// <summary>
    /// And an accessor nobody published says what is missing rather than handing back null for
    /// something else to trip over later.
    /// </summary>
    [Fact]
    public void A_surface_built_before_its_application_exists_says_so()
    {
        var accessor = new ApplicationHost.Accessor();

        Assert.Null(accessor.Host);
        var failure = Assert.Throws<InvalidOperationException>(() => accessor.Required);
        Assert.Contains("teardown", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The loose file a launch was asked to open belongs to the application it was asked of, not to
    /// the process.
    /// </summary>
    [Fact]
    public async Task The_file_a_launch_was_asked_to_open_belongs_to_that_application()
    {
        await using var first = ApplicationHost.Create(new AppDataPaths(Path.Combine(_dataRoot, "c")));
        await using var second = ApplicationHost.Create(new AppDataPaths(Path.Combine(_dataRoot, "d")));

        first.PendingActivationPath = Path.Combine(_dataRoot, "c", "film.mkv");

        Assert.Null(second.PendingActivationPath);
    }
}
