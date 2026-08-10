// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Data;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Time;
using ApSolutions.LocalMedia.Presentation.Navigation;
using ApSolutions.LocalMedia.Presentation.Shell;
using ApSolutions.LocalMedia.Windows.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// The floor everything else stands on: the database and its migrations, the repositories that
    /// read it, the file system as this application sees it, the clock, and the event publisher.
    /// </summary>
    /// <remarks>
    /// ARQ-006 step 2. These registrations used to be the first eighty lines of a three-hundred-line
    /// chain, which meant the only way to find out what a component depended on was to read all of
    /// it. Splitting by area is not decoration: each module is now short enough that a missing
    /// registration is visible.
    /// </remarks>
    private static IServiceCollection AddData(
        this IServiceCollection services,
        IAppDataPaths paths,
        ShellHost shellHost) =>
        services
            .AddSingleton<INavigationService, NavigationService>()
            .AddSingleton(paths)
            .AddSingleton(shellHost)
            // ARQ-001: one per container rather than a static, so two applications in one process
            // never reach each other's session. The host publishes itself here once it exists.
            .AddSingleton<ApplicationHost.Accessor>()
            .AddSingleton<SqliteConnectionFactory>()
            .AddSingleton<MigrationRunner>(provider => new MigrationRunner(
                provider.GetRequiredService<SqliteConnectionFactory>()))
            .AddSingleton<IntegrityChecker>()
            .AddSingleton<IDatabaseIntegrityChecker>(provider => provider.GetRequiredService<IntegrityChecker>())
            .AddSingleton<ILibraryRootRepository, LibraryRootRepository>()
            .AddSingleton<IMediaFileRepository, MediaFileRepository>()
            .AddSingleton<CatalogRepository>()
            // ICatalogRepository was a dead registration (LIB-002/003 follow-up): the application
            // projects titles through the scan's own writes, and nothing ever resolved the
            // interface. Removed rather than left silent, the ARQ-A01 rule.
            .AddSingleton<ICatalogQueryService>(provider => provider.GetRequiredService<CatalogRepository>())
            .AddSingleton<IPathNormalizer, WindowsPathNormalizer>()
            .AddSingleton<IMediaFileEnumerator, MediaFileEnumerator>()
            .AddSingleton<IMediaProbe, LibVlcMediaProbe>()
            .AddSingleton<IClock, SystemClock>()
            .AddSingleton<IRootWatcher>(provider => new DebouncedFileWatcher(
                provider.GetRequiredService<IClock>()))
            .AddSingleton<IFallbackScanScheduler>(provider => new FallbackScanScheduler(
                provider.GetRequiredService<IClock>(),
                FallbackScanScheduler.DefaultRecoveryInterval))
            .AddSingleton<InProcessApplicationEventPublisher>()
            .AddSingleton<IApplicationEventPublisher>(provider =>
                provider.GetRequiredService<InProcessApplicationEventPublisher>());
}
