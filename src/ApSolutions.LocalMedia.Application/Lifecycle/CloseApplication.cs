// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Lifecycle;

namespace ApSolutions.LocalMedia.Application.Lifecycle;

/// <summary>Where the lifecycle choices are kept between sessions.</summary>
public interface ILifecycleSettings
{
    LifecyclePreferences Current { get; }

    void Save(LifecyclePreferences updated);
}

/// <summary>
/// Closing the window, in the one order that is allowed.
/// <para>
/// The position reaches storage before the session is torn down and before the window goes anywhere.
/// Everything after that can be repeated; a lost position cannot, so it never waits behind anything.
/// </para>
/// </summary>
public sealed class CloseApplication
{
    private readonly ILifecycleSettings _settings;
    private readonly ITrayService _tray;
    private readonly Func<Task> _persistProgress;
    private readonly Func<Task> _stopPlayback;
    private readonly Action _hideWindow;
    private readonly Action _exitApplication;

    public CloseApplication(
        ILifecycleSettings settings,
        ITrayService tray,
        Func<Task> persistProgress,
        Func<Task> stopPlayback,
        Action hideWindow,
        Action? exitApplication = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _tray = tray ?? throw new ArgumentNullException(nameof(tray));
        _persistProgress = persistProgress ?? throw new ArgumentNullException(nameof(persistProgress));
        _stopPlayback = stopPlayback ?? throw new ArgumentNullException(nameof(stopPlayback));
        _hideWindow = hideWindow ?? throw new ArgumentNullException(nameof(hideWindow));
        _exitApplication = exitApplication ?? _hideWindow;
    }

    public Task<CloseDecision> ExecuteAsync(
        bool hasActivePlayback,
        CancellationToken cancellationToken = default) =>
        RunAsync(AppLifecyclePolicy.ResolveClose(_settings.Current, hasActivePlayback), cancellationToken);

    /// <summary>
    /// Ending the application outright, which is what the tray's exit entry asks for. It takes the
    /// same path as a close, so the position is written first there too: leaving through the tray
    /// must not be a way to lose a minute of playback.
    /// </summary>
    public Task<CloseDecision> ExitAsync(
        bool hasActivePlayback,
        CancellationToken cancellationToken = default) =>
        RunAsync(
            AppLifecyclePolicy.ResolveClose(LifecyclePreferences.Default, hasActivePlayback),
            cancellationToken);

    private async Task<CloseDecision> RunAsync(CloseDecision decision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _persistProgress().ConfigureAwait(false);

        if (decision.StopPlayback)
        {
            await _stopPlayback().ConfigureAwait(false);
        }

        if (decision.HideToTray)
        {
            _tray.Show();
            _hideWindow();
        }
        else
        {
            _tray.Hide();
            _exitApplication();
        }

        return decision;
    }
}
