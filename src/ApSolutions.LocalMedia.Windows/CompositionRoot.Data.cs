// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Application.Data;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Events;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Data.Repositories;
using ApSolutions.LocalMedia.Infrastructure.FileSystem;
using ApSolutions.LocalMedia.Infrastructure.Media;
using ApSolutions.LocalMedia.Infrastructure.Time;
using ApSolutions.LocalMedia.Presentation.Courses;
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
            // ICatalogRepository was removed as a dead registration once (LIB-002/003 follow-up):
            // nothing in the application resolved it, so it went rather than being left silent —
            // the ARQ-A01 rule. It is back on 2026-08-25 with the consumer it was missing:
            // GroupScannedEpisodes writes a show, its seasons and its episodes through it, which is
            // how a folder of episodes becomes one card. Registered and fed, this time.
            .AddSingleton<ICatalogRepository>(provider => provider.GetRequiredService<CatalogRepository>())
            .AddSingleton<ICatalogQueryService>(provider => provider.GetRequiredService<CatalogRepository>())
            // Courses (CRS-001..CRS-005). One adapter answers both course ports - the depth a root
            // declares is a column on `library_roots`, so splitting it into a store of its own would
            // be a second class over one table.
            //
            // `ICourseRootDeclarationStore` and `MarkCoursesInRoot` were deliberately absent until
            // 2026-08-31: nothing resolved them, and a service nobody resolves is this repository's
            // own characteristic defect - ServiceConsumptionTests said so out loud when they were.
            // What resolves them now is the add dialog's «Curso (carpeta de lecciones)» half, which
            // reaches them through DeclareCourseFolder. Registered and fed, this time as well.
            .AddSingleton<CourseRepository>()
            .AddSingleton<ICourseRepository>(provider => provider.GetRequiredService<CourseRepository>())
            .AddSingleton<ICourseRootDeclarationStore>(provider =>
                provider.GetRequiredService<CourseRepository>())
            .AddSingleton<MarkCoursesInRoot>()
            .AddSingleton<DeclareCourseFolder>()
            .AddSingleton<ICourseLessonReader, CourseLessonReader>()
            .AddSingleton<GetCourses>()
            // CRS-004. What resolves these two is the player: the session asks whether the file it
            // opened is a lesson, and the end of a lesson asks what comes after it. Both are fed the
            // moment they are registered, which is the rule the comment above was written for.
            .AddSingleton<GetLessonSession>()
            .AddTransient<StartNextLessonCountdown>()
            .AddSingleton<CoursesViewModel>()
            .AddSingleton<CourseDetailsViewModel>()
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
