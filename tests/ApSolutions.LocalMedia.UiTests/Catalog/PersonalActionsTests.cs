// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Xml.Linq;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Personalization;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Catalog;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Catalog;

/// <summary>
/// The three personal actions on a card or a details page. Every state is announced in words, so none
/// of them is a colour-only difference.
/// </summary>
public sealed class PersonalActionsTests
{
    private static readonly ContentKey Content = ContentKey.ForTitle(
        new TitleId(Guid.Parse("f2000000-0000-4000-8000-000000000001")));

    [Fact]
    public void An_unmarked_item_shows_all_three_actions_in_their_off_state()
    {
        var viewModel = new PersonalActionsViewModel();

        viewModel.Apply(PersonalState.Empty(Content));

        Assert.False(viewModel.IsFavorite);
        Assert.False(viewModel.IsWatchLater);
        Assert.False(viewModel.HasRating);
        Assert.Equal(string.Empty, viewModel.RatingText);
        Assert.False(viewModel.ClearRatingCommand.CanExecute(null));
    }

    [Fact]
    public void Each_command_reports_exactly_what_changed_to_the_host()
    {
        var requests = new List<PersonalActionRequest>();
        var viewModel = new PersonalActionsViewModel(request =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        });
        viewModel.Apply(PersonalState.Empty(Content));

        viewModel.ToggleFavoriteCommand.Execute(null);
        viewModel.ToggleWatchLaterCommand.Execute(null);
        viewModel.SetRatingCommand.Execute(8);
        viewModel.Apply(PersonalState.Empty(Content).WithRating(8));
        viewModel.ClearRatingCommand.Execute(null);

        Assert.Equal(
            [
                new PersonalActionRequest(PersonalActionKind.ToggleFavorite, null),
                new PersonalActionRequest(PersonalActionKind.ToggleWatchLater, null),
                new PersonalActionRequest(PersonalActionKind.SetRating, 8),
                new PersonalActionRequest(PersonalActionKind.SetRating, null),
            ],
            requests);
    }

    [Fact]
    public void A_rating_outside_one_to_ten_never_reaches_the_host()
    {
        var requests = new List<PersonalActionRequest>();
        var viewModel = new PersonalActionsViewModel(request =>
        {
            requests.Add(request);
            return Task.CompletedTask;
        });
        viewModel.Apply(PersonalState.Empty(Content));

        Assert.False(viewModel.SetRatingCommand.CanExecute(0));
        Assert.False(viewModel.SetRatingCommand.CanExecute(11));
        Assert.False(viewModel.SetRatingCommand.CanExecute("eight"));
        Assert.True(viewModel.SetRatingCommand.CanExecute(1));
        Assert.True(viewModel.SetRatingCommand.CanExecute(10));

        viewModel.SetRatingCommand.Execute(0);
        viewModel.SetRatingCommand.Execute(11);
        Assert.Empty(requests);
    }

    [Fact]
    public void Applying_a_stored_state_updates_every_visible_fact()
    {
        var viewModel = new PersonalActionsViewModel();
        var changed = new List<string>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);

        viewModel.Apply(PersonalState.Empty(Content)
            .WithFavorite(true)
            .WithWatchLater(true)
            .WithRating(6));

        Assert.True(viewModel.IsFavorite);
        Assert.True(viewModel.IsWatchLater);
        Assert.True(viewModel.HasRating);
        Assert.Equal(6, viewModel.Rating);
        Assert.Equal("6", viewModel.RatingText);
        Assert.True(viewModel.ClearRatingCommand.CanExecute(null));
        Assert.Contains(nameof(PersonalActionsViewModel.IsFavorite), changed, StringComparer.Ordinal);
        Assert.Contains(nameof(PersonalActionsViewModel.RatingText), changed, StringComparer.Ordinal);
    }

    [Fact]
    public void The_view_model_rejects_a_missing_state()
    {
        Assert.Throws<ArgumentNullException>(() => new PersonalActionsViewModel().Apply(null!));
    }

    [AvaloniaFact]
    public void Every_control_has_an_accessible_name_takes_focus_and_states_itself_in_words()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current, CultureInfo.GetCultureInfo("es-ES"));
        var viewModel = new PersonalActionsViewModel();
        viewModel.Apply(PersonalState.Empty(Content).WithFavorite(true).WithRating(4));
        var view = new PersonalActionsView { DataContext = viewModel };
        var window = new Window { Width = 900, Height = 400, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = view.GetVisualDescendants().OfType<Button>().ToArray();
        Assert.NotEmpty(buttons);
        foreach (var button in buttons)
        {
            Assert.True(button.Focusable);
            Assert.False(
                string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)),
                "A personal action has no accessible name.");
        }

        // State is announced as text, not by colour alone.
        var texts = view.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.IsVisible && !string.IsNullOrWhiteSpace(text.Text))
            .Select(text => text.Text!)
            .ToArray();
        Assert.NotEmpty(texts);
        window.Close();
    }

    [Fact]
    public void The_personal_actions_view_carries_no_literal_text()
    {
        var path = Path.Combine(
            RepositoryLayout.Root,
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Catalog",
            "PersonalActionsView.axaml");
        Assert.True(File.Exists(path), $"The personal actions view is missing: {path}");

        var literals = XDocument.Load(path)
            .Descendants()
            .Attributes()
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "PlaceholderText")
            .Where(attribute => !attribute.Value.TrimStart().StartsWith('{'))
            .Select(attribute => $"{attribute.Name.LocalName}={attribute.Value}")
            .ToArray();
        Assert.Empty(literals);
    }
}
