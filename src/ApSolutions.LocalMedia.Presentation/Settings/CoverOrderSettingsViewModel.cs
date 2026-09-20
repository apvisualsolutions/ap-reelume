// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>One origin as a row of the list that can be moved (LIB-021).</summary>
/// <param name="Origin">Which of the three places a cover can come from.</param>
/// <param name="NameKey">
/// The resource key of its name, not the name. The language is changed while the application runs,
/// and words resolved when this panel was built would stay in the old one; the view resolves the key
/// through <c>ResourceKeyConverter</c>, which is what the recommendation rail does with its reasons.
/// </param>
public sealed record CoverOriginRow(CoverOrigin Origin, string NameKey);

/// <summary>
/// Moving the order every title's cover is looked for in, for the whole library (ADR-0009 decision
/// 4). A title can override it from its own editor.
/// </summary>
/// <remarks>
/// It reorders with two buttons over the selected row rather than by dragging, and that is not a
/// shortcut: a drag cannot be clicked by the autonomous walk — the headless hit test does not follow
/// a gesture — and it cannot be done with a keyboard at all. Two buttons over a list is also what
/// Windows itself draws for its own priority lists.
/// <para>
/// Every move stores at once, with no «apply». That is what the other settings groups do, and a
/// panel that waits for a button nobody drew would reorder its own list and store nothing.
/// </para>
/// </remarks>
public sealed class CoverOrderSettingsViewModel : INotifyPropertyChanged
{
    private readonly ICoverOrderSettings _settings;
    private readonly ObservableCollection<CoverOriginRow> _origins;
    private readonly RelayCommand _moveUp;
    private readonly RelayCommand _moveDown;
    private int _selectedIndex;

    public CoverOrderSettingsViewModel(ICoverOrderSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _origins = [.. Rows(_settings.Current)];
        _moveUp = new RelayCommand(() => Move(-1), () => CanMove(-1));
        _moveDown = new RelayCommand(() => Move(1), () => CanMove(1));
        RestoreDefaultsCommand = new RelayCommand(RestoreDefaults, () => true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The three origins, in the order they are looked for.</summary>
    public IReadOnlyList<CoverOriginRow> Origins => _origins;

    /// <summary>
    /// The row the two buttons act on. It opens on the first one rather than on nothing so that the
    /// panel can be used without a click to select first.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) { return; }

            _selectedIndex = value;
            OnPropertyChanged(nameof(SelectedIndex));
            RaiseMoves();
        }
    }

    public ICommand MoveUpCommand => _moveUp;

    public ICommand MoveDownCommand => _moveDown;

    /// <summary>The «Restaurar valores por defecto» of this group (UX-010).</summary>
    public ICommand RestoreDefaultsCommand { get; }

    private static IEnumerable<CoverOriginRow> Rows(IReadOnlyList<CoverOrigin> order) =>
        CoverOrderPolicy.Normalize(order).Select(origin => new CoverOriginRow(origin, "CoverOrigin" + origin));

    private bool CanMove(int direction)
    {
        var target = _selectedIndex + direction;
        return _selectedIndex >= 0 && target >= 0 && target < _origins.Count;
    }

    private void Move(int direction)
    {
        // A command can be executed by a keyboard or a binding even while its button is disabled, so
        // the refusal lives here as well as in CanExecute.
        if (!CanMove(direction)) { return; }

        var from = _selectedIndex;
        _origins.Move(from, from + direction);
        SelectedIndex = from + direction;
        Store();
    }

    private void RestoreDefaults()
    {
        _origins.Clear();
        foreach (var row in Rows(CoverOrderPolicy.Default))
        {
            _origins.Add(row);
        }

        Store();
    }

    private void Store() => _settings.Save([.. _origins.Select(row => row.Origin)]);

    private void RaiseMoves()
    {
        _moveUp.Raise();
        _moveDown.Raise();
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed class RelayCommand(Action execute, Func<bool> canExecute) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => canExecute();

        public void Execute(object? parameter) => execute();

        /// <summary>
        /// Said out loud when the selection moves. Without it the button that cannot be pressed any
        /// more stays looking pressable until something else happens to redraw the panel.
        /// </summary>
        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
