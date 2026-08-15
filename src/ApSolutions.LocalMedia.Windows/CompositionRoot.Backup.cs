// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Backup;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Infrastructure.Backup;
using ApSolutions.LocalMedia.Infrastructure.Data;
using ApSolutions.LocalMedia.Infrastructure.Metadata;
using ApSolutions.LocalMedia.Infrastructure.Privacy;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.Presentation.Backup;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Shell;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// Where preferences live, how a library is copied out and brought back, and what the
    /// application is willing to say about itself.
    /// </summary>
    /// <remarks>
    /// ARQ-006 step 2. Backup and privacy share a module because they answer the same question from
    /// two sides: what leaves this machine, and under whose decision.
    /// </remarks>
    private static IServiceCollection AddSettingsAndBackup(this IServiceCollection services) =>
        services
            .AddSingleton<ISettingsStore>(provider => new JsonSettingsStore(
                provider.GetRequiredService<IAppDataPaths>().SettingsPath))
            .AddSingleton<MainWindowPlacement>()
            .AddSingleton<IBackupSnapshotWriter>(provider => new SqliteBackupService(
                provider.GetRequiredService<SqliteConnectionFactory>()))
            .AddSingleton<IBackupStore>(provider => new RotatingBackupStore(
                provider.GetRequiredService<IAppDataPaths>().BackupsDirectory))
            .AddSingleton<IBackupArchiveWriter, ZipExportService>()
            .AddSingleton(provider => new CreateBackup(
                provider.GetRequiredService<IBackupSnapshotWriter>(),
                provider.GetRequiredService<IBackupStore>(),
                provider.GetRequiredService<IAppDataPaths>(),
                provider.GetRequiredService<ILibraryRootRepository>(),
                provider.GetRequiredService<IClock>(),
                GetAppVersion()))
            .AddSingleton<ExportLibrary>()
            .AddSingleton<IPrivacySettings, StoredPrivacySettings>()

            // ARQ-004. One per application, so two of them in one process do not write each other's
            // failures into a log the other owns. It is read where the diagnostics inputs are built.
            .AddSingleton<ISessionFailureLog, InMemorySessionFailureLog>()
            .AddSingleton<IDiagnosticsBuilder, AllowlistedDiagnosticsBuilder>()
            .AddSingleton<CreateDiagnostics>()
            .AddTransient(provider => new PrivacySettingsViewModel(
                provider.GetRequiredService<IPrivacySettings>(),
                provider.GetRequiredService<IDiagnosticsBuilder>(),
                () => DescribeThisMachine(provider),
                (consent, inputs, cancellationToken) => provider.GetRequiredService<CreateDiagnostics>()
                    .ExportAsync(consent, inputs, cancellationToken),
                () => provider.GetRequiredService<IClock>().UtcNow,
                NetworkPurposeRegistry.Declared,
                cancellationToken => provider.GetRequiredService<CreateDiagnostics>()
                    .DiscardAsync(cancellationToken),
                provider.GetRequiredService<IAutoRefreshSettings>(),

                // LIB-016. The same consent the candidate source asks about, read the same way: the
                // token in place is the deliberate, revocable act, and without it the automatic
                // refresh has nothing it could do — so its switch is not offered.
                () => provider.GetRequiredService<TmdbOptions>().AccessToken is not null))
            .AddSingleton<IBackupValidator, BackupValidator>()
            .AddSingleton<IStagedRestoreService>(provider => new StagedRestoreService(
                provider.GetRequiredService<IAppDataPaths>()))
            .AddSingleton<PreviewRestore>()
            .AddSingleton<RestoreBackup>()
            .AddTransient(provider => new RestoreWizardViewModel(
                (archive, remaps, cancellationToken) => provider.GetRequiredService<PreviewRestore>()
                    .ExecuteAsync(archive, remaps, cancellationToken),
                (archive, remaps, progress, cancellationToken) => provider.GetRequiredService<RestoreBackup>()
                    .ExecuteAsync(archive, remaps, progress, cancellationToken),
                ChooseArchiveSourceAsync))
            .AddTransient(provider => new BackupViewModel(
                (progress, cancellationToken) => provider.GetRequiredService<CreateBackup>()
                    .ExecuteAsync(progress, cancellationToken),
                (destination, progress, cancellationToken) => provider.GetRequiredService<ExportLibrary>()
                    .ExecuteAsync(destination, progress, cancellationToken),
                ChooseArchiveDestinationAsync));
}
