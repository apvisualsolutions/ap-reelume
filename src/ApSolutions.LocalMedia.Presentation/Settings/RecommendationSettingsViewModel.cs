// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Continuity;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// What recommendations run on: whether to make them at all, and the point at which content counts
/// as watched. Turning them off is remembered and skips the computation entirely rather than hiding
/// its result; moving the threshold reconsiders only the states the player computed, never a
/// decision made by hand.
/// </summary>
public sealed class RecommendationSettingsViewModel : INotifyPropertyChanged
{
    private readonly IRecommendationSettings _settings;
    private readonly ConfigureWatchedThreshold? _watchedThreshold;
    private double _watchedThresholdPercent;
    private int _recalculatedCount;
    private bool _hasThresholdResult;

    public RecommendationSettingsViewModel(
        IRecommendationSettings settings,
        ConfigureWatchedThreshold? watchedThreshold = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _watchedThreshold = watchedThreshold;
        _watchedThresholdPercent = Math.Round(
            (watchedThreshold?.Current ?? WatchStatePolicy.DefaultWatchedThreshold) * 100);
        ApplyWatchedThresholdCommand = new AsyncRelayCommand(ApplyWatchedThresholdAsync);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsEnabled
    {
        get => _settings.IsEnabled;
        set
        {
            if (value == _settings.IsEnabled)
            {
                return;
            }

            _settings.SetEnabled(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDisabled));
        }
    }

    public bool IsDisabled => !IsEnabled;

    /// <summary>True when the assembled application handed this surface the threshold to configure.</summary>
    public bool HasWatchedThreshold => _watchedThreshold is not null;

    /// <summary>The threshold as a percentage, which is how the slider and the sentence say it.</summary>
    public double WatchedThresholdPercent
    {
        get => _watchedThresholdPercent;
        set
        {
            // Whole percents are the only granularity the surface offers or the sentence can say.
            var rounded = Math.Round(value);
            if (rounded == _watchedThresholdPercent)
            {
                return;
            }

            _watchedThresholdPercent = rounded;
            OnPropertyChanged();
        }
    }

    public static double MinimumWatchedThresholdPercent => WatchStatePolicy.MinimumWatchedThreshold * 100;

    public static double MaximumWatchedThresholdPercent => WatchStatePolicy.MaximumWatchedThreshold * 100;

    public ICommand ApplyWatchedThresholdCommand { get; }

    /// <summary>How many automatic states the last apply moved; manual decisions never count here.</summary>
    public int RecalculatedCount
    {
        get => _recalculatedCount;
        private set
        {
            _recalculatedCount = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True once an apply has run, so the result sentence only exists when it is true.</summary>
    public bool HasThresholdResult
    {
        get => _hasThresholdResult;
        private set
        {
            _hasThresholdResult = value;
            OnPropertyChanged();
        }
    }

    private async Task ApplyWatchedThresholdAsync()
    {
        if (_watchedThreshold is null)
        {
            return;
        }

        RecalculatedCount = await _watchedThreshold
            .ExecuteAsync(_watchedThresholdPercent / 100d)
            .ConfigureAwait(true);
        HasThresholdResult = true;

        // The use case clamps nonsense; the slider shows what is actually in force afterwards.
        _watchedThresholdPercent = Math.Round(_watchedThreshold.Current * 100);
        OnPropertyChanged(nameof(WatchedThresholdPercent));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
