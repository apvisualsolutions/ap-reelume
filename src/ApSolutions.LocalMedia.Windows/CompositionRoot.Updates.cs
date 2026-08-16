// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Lifecycle;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Application.Storage;
using ApSolutions.LocalMedia.Application.Updates;
using ApSolutions.LocalMedia.Domain.Common;
using ApSolutions.LocalMedia.Domain.Updates;
using ApSolutions.LocalMedia.Infrastructure.Settings;
using ApSolutions.LocalMedia.Infrastructure.Updates;
using ApSolutions.LocalMedia.Presentation.Language;
using ApSolutions.LocalMedia.Presentation.Settings;
using ApSolutions.LocalMedia.Presentation.Theme;
using ApSolutions.LocalMedia.Presentation.Updates;
using ApSolutions.LocalMedia.Windows.Accessibility;
using ApSolutions.LocalMedia.Windows.Startup;
using ApSolutions.LocalMedia.Windows.Tray;
using ApSolutions.LocalMedia.Windows.Updates;
using ApSolutions.LocalMedia.Windows.Windowing;
using Microsoft.Extensions.DependencyInjection;

namespace ApSolutions.LocalMedia.Windows;

public static partial class CompositionRoot
{
    /// <summary>
    /// Asking whether there is a newer version, fetching it, and confirming it — three services
    /// because they answer to three different acts.
    /// </summary>
    /// <remarks>
    /// ARQ-006 step 2. Nothing here runs on its own: the automatic check is off until somebody turns
    /// it on, and the launcher is only ever reached through a consent naming the version.
    /// </remarks>
    private static IServiceCollection AddUpdates(this IServiceCollection services) =>
        services
            .AddSingleton<IUpdateSettings, StoredUpdateSettings>()
            .AddSingleton(_ => new InstalledRelease(GetAppVersion(), GetRuntimeIdentifier()))
            .AddSingleton<IUpdateSource>(_ => new GitHubReleaseUpdateProvider(
                CreateUpdateClient(new Uri("https://api.github.com/")),
                UpdateRepositoryOwner,
                UpdateRepositoryName,
                UpdateSigningKey.PublicKey))
            .AddSingleton<IUpdateDownloader>(provider => new VerifiedUpdateDownloader(
                CreateUpdateClient(baseAddress: null),
                Path.Combine(provider.GetRequiredService<IAppDataPaths>().DataRoot, "updates")))
            .AddSingleton<IUpdateLauncher>(_ => new WindowsUpdateLauncher(OpenWithWindows))
            .AddSingleton<CheckForUpdates>()
            .AddSingleton<ConfirmUpdate>()
            // One instance, not one per resolution: the window starts the automatic check on the
            // same surface the shell is showing. A transient here would check for updates on a view
            // model nobody can see, which is indistinguishable from not checking at all.
            .AddSingleton(provider => new UpdateViewModel(
                (trigger, cancellationToken) => provider.GetRequiredService<CheckForUpdates>()
                    .ExecuteAsync(trigger, cancellationToken),
                (release, progress, cancellationToken) => provider.GetRequiredService<ConfirmUpdate>()
                    .StageAsync(release, progress, cancellationToken),
                (staged, consent, cancellationToken) => provider.GetRequiredService<ConfirmUpdate>()
                    .ExecuteAsync(staged, consent, cancellationToken),
                () => provider.GetRequiredService<IClock>().UtcNow,
                provider.GetRequiredService<IUpdateSettings>()));

    /// <summary>
    /// How the application looks, which language it speaks, and how it behaves around the desktop:
    /// the tray, sign-in startup, the backdrop, and reduced motion.
    /// </summary>
    /// <remarks>ARQ-006 step 2.</remarks>
    private static IServiceCollection AddAppearanceAndLifecycle(this IServiceCollection services) =>
        services
            .AddSingleton<IRecommendationSettings, StoredRecommendationSettings>()
            .AddTransient<RecommendationSettingsViewModel>()
            .AddSingleton<ILifecycleSettings, StoredLifecycleSettings>()
            .AddSingleton<IStartupService>(provider => new WindowsStartupService(
                GetExecutablePath(),
                provider.GetRequiredService<IAppDataPaths>().StartupRegistrySubKey))
            .AddSingleton<ITrayService>(_ => new WindowsTrayService(
                ReadResource("ProductDisplayName"),
                ReadResource("LifecycleTrayOpenAction"),
                ReadResource("LifecycleTrayExitAction")))
            .AddTransient<LifecycleSettingsViewModel>()
            .AddSingleton<IBackdropService, MicaBackdropService>()
            .AddSingleton<IReducedMotionService, WindowsReducedMotionService>()
            .AddSingleton<IThemeService>(provider => new FluentThemeService(
                Avalonia.Application.Current ?? throw new InvalidOperationException("Avalonia application is not initialized."),
                provider.GetRequiredService<ISettingsStore>(),
                provider.GetRequiredService<IBackdropService>(),
                provider.GetRequiredService<IReducedMotionService>()))
            // One answer to "which language?" (BUG-011): the window, the updater's summary, and the
            // metadata language all read what this service applied, never the machine's culture.
            .AddSingleton<ILanguageService>(provider => new StoredLanguageService(
                provider.GetRequiredService<ISettingsStore>(),
                Avalonia.Application.Current ?? throw new InvalidOperationException("Avalonia application is not initialized.")))
            .AddTransient<AppearanceSettingsViewModel>()
            .AddTransient<ScanSettingsViewModel>();
}
