// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Catalog;

/// <summary>Which of the three personal marks the person just changed.</summary>
public enum PersonalActionKind
{
    ToggleFavorite,
    ToggleWatchLater,
    SetRating,
}

/// <summary>
/// What the surface asks the host to record. A rating change carries its value; a null rating clears
/// it, which is a deliberate statement rather than an absent one.
/// </summary>
public sealed record PersonalActionRequest(PersonalActionKind Kind, int? Rating);

/// <summary>
/// Favourite, watch later, and rating on one piece of content. Each state is announced in words as
/// well as shown, so none of the three is a colour-only difference.
/// </summary>
public sealed class PersonalActionsViewModel : INotifyPropertyChanged
{
    private readonly Func<PersonalActionRequest, Task>? _onChanged;
    private PersonalState _state = PersonalState.Empty(
        ContentKey.ForTitle(new Domain.Catalog.TitleId(Guid.Empty)));

    public PersonalActionsViewModel(Func<PersonalActionRequest, Task>? onChanged = null)
    {
        _onChanged = onChanged;
        ToggleFavoriteCommand = new AsyncRelayCommand(
            _ => RequestAsync(new PersonalActionRequest(PersonalActionKind.ToggleFavorite, null)));
        ToggleWatchLaterCommand = new AsyncRelayCommand(
            _ => RequestAsync(new PersonalActionRequest(PersonalActionKind.ToggleWatchLater, null)));
        SetRatingCommand = new AsyncRelayCommand(
            parameter => RequestAsync(new PersonalActionRequest(
                PersonalActionKind.SetRating,
                ReadRating(parameter))),
            parameter => ReadRating(parameter) is not null);
        ClearRatingCommand = new AsyncRelayCommand(
            _ => RequestAsync(new PersonalActionRequest(PersonalActionKind.SetRating, null)),
            _ => HasRating);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ToggleFavoriteCommand { get; }

    public ICommand ToggleWatchLaterCommand { get; }

    public ICommand SetRatingCommand { get; }

    public ICommand ClearRatingCommand { get; }

    /// <summary>The ten scores the surface offers, so the range never has to be written by hand.</summary>
    public IReadOnlyList<int> RatingChoices { get; } =
        [.. Enumerable.Range(PersonalStatePolicy.MinimumRating, PersonalStatePolicy.MaximumRating)];

    public bool IsFavorite => _state.IsFavorite;

    public bool IsNotFavorite => !_state.IsFavorite;

    public bool IsWatchLater => _state.IsWatchLater;

    public bool IsNotWatchLater => !_state.IsWatchLater;

    public int? Rating => _state.Rating;

    public bool HasRating => _state.HasRating;

    public bool HasNoRating => !_state.HasRating;

    public string RatingText => _state.Rating is { } rating
        ? rating.ToString(CultureInfo.CurrentCulture)
        : string.Empty;

    /// <summary>Applies what the repository holds; the surface never decides the marks itself.</summary>
    public void Apply(PersonalState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        foreach (var name in new[]
        {
            nameof(IsFavorite),
            nameof(IsNotFavorite),
            nameof(IsWatchLater),
            nameof(IsNotWatchLater),
            nameof(Rating),
            nameof(HasRating),
            nameof(HasNoRating),
            nameof(RatingText),
        })
        {
            OnPropertyChanged(name);
        }

        (ClearRatingCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>A value outside one to ten, or anything that is not a number, is not a rating.</summary>
    private static int? ReadRating(object? parameter)
    {
        var rating = parameter switch
        {
            int value => value,
            string text when int.TryParse(text, CultureInfo.CurrentCulture, out var parsed) => parsed,
            _ => (int?)null,
        };
        return rating is not null && PersonalStatePolicy.IsValidRating(rating) ? rating : null;
    }

    private Task RequestAsync(PersonalActionRequest request) =>
        _onChanged?.Invoke(request) ?? Task.CompletedTask;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
