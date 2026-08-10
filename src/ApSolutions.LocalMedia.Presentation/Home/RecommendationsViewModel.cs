// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>
/// The recommendations rail. Every card carries the reason keys behind it, so the "why" the person
/// reads is translated text rather than a number nobody can interpret.
/// </summary>
public sealed class RecommendationsViewModel : INotifyPropertyChanged
{
    private readonly GetRecommendations _getRecommendations;
    private readonly IRecommendationSettings _settings;
    private readonly Func<TitleId, string> _titleLookup;
    private IReadOnlyList<RecommendationItemViewModel> _items = [];

    public RecommendationsViewModel(
        GetRecommendations getRecommendations,
        IRecommendationSettings settings,
        Func<TitleId, string>? titleLookup = null)
    {
        _getRecommendations = getRecommendations ?? throw new ArgumentNullException(nameof(getRecommendations));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _titleLookup = titleLookup ?? (_ => string.Empty);
        ToggleCommand = new AsyncRelayCommand(
            () => SetEnabledAsync(!IsEnabled, CancellationToken.None));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ToggleCommand { get; }

    public bool IsEnabled => _settings.IsEnabled;

    public bool IsDisabled => !IsEnabled;

    public IReadOnlyList<RecommendationItemViewModel> Items
    {
        get => _items;
        private set => SetField(ref _items, value);
    }

    public bool HasRecommendations => Items.Count > 0;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var results = await _getRecommendations
            .ExecuteAsync(new RecommendationOptions(IsEnabled, Limit: 20), cancellationToken)
            .ConfigureAwait(false);
        Items = [.. results.Select(result => new RecommendationItemViewModel(result, _titleLookup(result.ContentId)))];
        OnPropertyChanged(nameof(HasRecommendations));
    }

    /// <summary>Stores the choice and reloads; switched off, the rail empties instead of hiding a result.</summary>
    public async Task SetEnabledAsync(bool isEnabled, CancellationToken cancellationToken = default)
    {
        _settings.SetEnabled(isEnabled);
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsDisabled));
        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>One suggestion, with the resource keys that explain it.</summary>
public sealed class RecommendationItemViewModel(Recommendation recommendation, string title)
{
    private readonly Recommendation _recommendation = recommendation
        ?? throw new ArgumentNullException(nameof(recommendation));

    public TitleId ContentId => _recommendation.ContentId;

    public string Title { get; } = title ?? string.Empty;

    public double Score => _recommendation.Score;

    /// <summary>Resource keys for the signals behind this suggestion, heaviest first.</summary>
    public IReadOnlyList<string> ReasonKeys { get; } =
        [.. recommendation.ReasonCodes.Select(reason => $"RecommendationReason{reason}")];

    public bool HasReasons => ReasonKeys.Count > 0;
}
