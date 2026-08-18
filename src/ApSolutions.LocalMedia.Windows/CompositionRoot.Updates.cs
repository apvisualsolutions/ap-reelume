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
using ApSolutions.LocalMedia.Windows.Shell;
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
            // Which source answers is decided by the resolved data root, once, here — the same
            // choice every other handover makes. A harness must not ask GitHub about releases from
            // whichever machine is running the suite, so a run keeping its data somewhere of its own
            // is offered whatever its own handover folder describes, and nothing is contacted.
            .AddSingleton<IUpdateSource>(provider =>
                provider.GetRequiredService<IAppDataPaths>().SystemHandoffDirectory is { } handoff
                    ? new HandoffUpdateSource(handoff)
                    : new GitHubReleaseUpdateProvider(
                        CreateUpdateClient(new Uri("https://api.github.com/")),
                        UpdateRepositoryOwner,
                        UpdateRepositoryName,
                        UpdateSigningKey.PublicKey))
            // The same choice again, and what changes for an isolated run is the transport and the
            // allowlist — never the download itself. VerifiedUpdateDownloader does the work on both
            // sides, so the hash, the declared size and the staging under .partial are the ones a
            // person's installation uses rather than a harness's imitation of them.
            .AddSingleton<IUpdateDownloader>(provider =>
            {
                var paths = provider.GetRequiredService<IAppDataPaths>();
                var staging = Path.Combine(paths.DataRoot, "updates");
                return paths.SystemHandoffDirectory is { } handoff
                    ? new HandoffUpdateDownloader(handoff, staging)
                    : new VerifiedUpdateDownloader(CreateUpdateClient(baseAddress: null), staging);
            })
            // The fourth exit the isolation rule goes through: a run keeping its data somewhere of
            // its own writes down which package it would have handed over instead of starting an
            // installer on the machine measuring it. Which handover is built was decided once,
            // elsewhere, by the resolved data root.
            .AddSingleton<IUpdateLauncher>(provider => new WindowsUpdateLauncher(
                provider.GetRequiredService<ISystemHandoff>().TryOpenPackage))
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

            // The one that writes handovers down, resolved rather than built where it is needed:
            // there are two exits behind it now — the recovery screen's and the player's external
            // one — and two of these would be two locks over one file. It is only ever resolved on
            // the branch that has already found a handover folder, which is why asking for it
            // without one is a failure rather than a null.
            .AddSingleton(provider => new RecordingSystemHandoff(
                provider.GetRequiredService<IAppDataPaths>().SystemHandoffDirectory
                    ?? throw new InvalidOperationException(
                        "This run owns the profile, so it hands things to Windows rather than "
                            + "writing them down; nothing should have asked for the recorder.")))

            // What the recovery screen hands to Windows itself, decided by the data root, once,
            // here — the same choice the trailer link and the archive dialogs make. The person whose
            // profile this is gets Explorer and a real shutdown; a run keeping its data somewhere of
            // its own writes down what it would have handed over, because a harness that shut the
            // application down would end the suite pressing the button.
            .AddSingleton<ISystemHandoff>(provider =>
                provider.GetRequiredService<IAppDataPaths>().SystemHandoffDirectory is not null
                    ? provider.GetRequiredService<RecordingSystemHandoff>()
                    : new WindowsSystemHandoff(System.Diagnostics.Process.Start, ShutdownDesktop))
            .AddTransient<LifecycleSettingsViewModel>()
            .AddSingleton<IBackdropService, MicaBackdropService>()
            .AddSingleton<IReducedMotionService, WindowsReducedMotionService>()
            .AddSingleton<IHighContrastService, WindowsHighContrastService>()
            .AddSingleton<IThemeService>(provider => new FluentThemeService(
                Avalonia.Application.Current ?? throw new InvalidOperationException("Avalonia application is not initialized."),
                provider.GetRequiredService<ISettingsStore>(),
                provider.GetRequiredService<IBackdropService>(),
                provider.GetRequiredService<IReducedMotionService>(),
                provider.GetRequiredService<IHighContrastService>()))
            // One answer to "which language?" (BUG-011): the window, the updater's summary, and the
            // metadata language all read what this service applied, never the machine's culture.
            .AddSingleton<ILanguageService>(provider => new StoredLanguageService(
                provider.GetRequiredService<ISettingsStore>(),
                Avalonia.Application.Current ?? throw new InvalidOperationException("Avalonia application is not initialized.")))
            .AddTransient<AppearanceSettingsViewModel>()
            .AddTransient<ScanSettingsViewModel>();
}
