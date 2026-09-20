// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.ComponentModel;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Metadata;
using ApSolutions.LocalMedia.Presentation.Settings;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// The general cover order as somebody moves it (LIB-021, ADR-0009 decision 4).
/// </summary>
/// <remarks>
/// Every move stores at once, with no «apply» button, because that is what the other settings groups
/// do and what the walk expects when it clicks. The assertions are therefore about what reached the
/// store and not only about what the rows say: a panel that reorders its own list and stores nothing
/// looks right and is the defect this repository is named after.
/// </remarks>
public sealed class CoverOrderSettingsViewModelTests
{
    [Fact]
    public void The_rows_open_in_the_stored_order()
    {
        var view = new CoverOrderSettingsViewModel(
            new FakeSettings([CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider]));

        Assert.Equal(
            [CoverOrigin.Frame, CoverOrigin.Personal, CoverOrigin.Provider],
            view.Origins.Select(row => row.Origin));
    }

    /// <summary>
    /// The name travels as a resource key and not as words: the language is changed while the
    /// application runs, and words resolved when the panel was built would stay in the old one.
    /// </summary>
    [Fact]
    public void Every_row_carries_the_key_of_its_name()
    {
        var view = new CoverOrderSettingsViewModel(new FakeSettings(CoverOrderPolicy.Default));

        Assert.Equal(Enum.GetValues<CoverOrigin>().Length, view.Origins.Count);
        Assert.Equal(
            ["CoverOriginPersonal", "CoverOriginProvider", "CoverOriginFrame"],
            view.Origins.Select(row => row.NameKey));
    }

    [Fact]
    public void Moving_the_selected_row_up_stores_the_new_order_and_the_selection_follows_it()
    {
        var settings = new FakeSettings(CoverOrderPolicy.Default);
        var view = new CoverOrderSettingsViewModel(settings) { SelectedIndex = 1 };

        view.MoveUpCommand.Execute(null);

        Assert.Equal([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame], settings.Saved);
        Assert.Equal([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame], view.Origins.Select(row => row.Origin));
        Assert.Equal(0, view.SelectedIndex);
    }

    [Fact]
    public void Moving_down_stores_it_too_and_the_selection_follows_the_row()
    {
        var settings = new FakeSettings(CoverOrderPolicy.Default);
        var view = new CoverOrderSettingsViewModel(settings) { SelectedIndex = 0 };

        view.MoveDownCommand.Execute(null);

        Assert.Equal([CoverOrigin.Provider, CoverOrigin.Personal, CoverOrigin.Frame], settings.Saved);
        Assert.Equal(1, view.SelectedIndex);
    }

    [Fact]
    public void The_first_row_cannot_go_up_and_the_last_cannot_go_down()
    {
        var view = new CoverOrderSettingsViewModel(new FakeSettings(CoverOrderPolicy.Default));

        view.SelectedIndex = 0;
        Assert.False(view.MoveUpCommand.CanExecute(null));
        Assert.True(view.MoveDownCommand.CanExecute(null));

        view.SelectedIndex = view.Origins.Count - 1;
        Assert.True(view.MoveUpCommand.CanExecute(null));
        Assert.False(view.MoveDownCommand.CanExecute(null));
    }

    [Fact]
    public void With_nothing_selected_neither_button_can_be_pressed()
    {
        var view = new CoverOrderSettingsViewModel(new FakeSettings(CoverOrderPolicy.Default))
        {
            SelectedIndex = -1,
        };

        Assert.False(view.MoveUpCommand.CanExecute(null));
        Assert.False(view.MoveDownCommand.CanExecute(null));
    }

    /// <summary>
    /// A press the buttons say they refuse changes nothing if it arrives anyway — a keyboard or a
    /// binding can execute a command whose button is disabled.
    /// </summary>
    [Fact]
    public void A_move_that_cannot_happen_stores_nothing_and_leaves_the_rows_alone()
    {
        var settings = new FakeSettings(CoverOrderPolicy.Default);
        var view = new CoverOrderSettingsViewModel(settings) { SelectedIndex = 0 };

        view.MoveUpCommand.Execute(null);
        view.SelectedIndex = -1;
        view.MoveDownCommand.Execute(null);

        Assert.Null(settings.Saved);
        Assert.Equal(CoverOrderPolicy.Default, view.Origins.Select(row => row.Origin));
    }

    [Fact]
    public void Restoring_defaults_puts_the_order_back_and_stores_it()
    {
        var settings = new FakeSettings([CoverOrigin.Frame, CoverOrigin.Provider, CoverOrigin.Personal]);
        var view = new CoverOrderSettingsViewModel(settings);

        view.RestoreDefaultsCommand.Execute(null);

        Assert.Equal(CoverOrderPolicy.Default, settings.Saved);
        Assert.Equal(CoverOrderPolicy.Default, view.Origins.Select(row => row.Origin));
    }

    /// <summary>
    /// Moving a row tells the screen the buttons changed. Without it the one that cannot be pressed
    /// any more stays looking pressable until something else happens to redraw.
    /// </summary>
    [Fact]
    public void A_move_says_the_buttons_changed()
    {
        var view = new CoverOrderSettingsViewModel(new FakeSettings(CoverOrderPolicy.Default)) { SelectedIndex = 1 };
        var told = new List<string?>();
        ((INotifyPropertyChanged)view).PropertyChanged += (_, args) => told.Add(args.PropertyName);

        // Subscribed on purpose: the buttons raise CanExecuteChanged only when somebody is
        // listening, and the test above this one leaves that half unexercised — which is a branch
        // the coverage gate counts and, worse, the thing that leaves a disabled button looking
        // pressable on screen.
        var heard = 0;
        view.MoveUpCommand.CanExecuteChanged += (_, _) => heard++;
        view.MoveDownCommand.CanExecuteChanged += (_, _) => heard++;

        view.MoveUpCommand.Execute(null);

        Assert.Contains(nameof(CoverOrderSettingsViewModel.SelectedIndex), told);
        Assert.True(heard >= 2, $"the two buttons said nothing when the selection moved ({heard}).");
    }

    [Fact]
    public void A_panel_with_nothing_behind_it_is_refused()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new CoverOrderSettingsViewModel(null!));
    }

    private sealed class FakeSettings(IReadOnlyList<CoverOrigin> current) : ICoverOrderSettings
    {
        private IReadOnlyList<CoverOrigin> _current = current;

        /// <summary>What reached the store, or nothing when nothing did.</summary>
        public IReadOnlyList<CoverOrigin>? Saved { get; private set; }

        public IReadOnlyList<CoverOrigin> Current => _current;

        public void Save(IReadOnlyList<CoverOrigin> order)
        {
            Saved = order;
            _current = order;
        }
    }
}
