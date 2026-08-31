// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Courses;
using ApSolutions.LocalMedia.Domain.Discovery;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Onboarding;

/// <summary>
/// The add dialog's «Curso (carpeta de lecciones)» half (CRS-001, ADR-0006 amendment 1).
/// </summary>
/// <remarks>
/// It owns the choice between the two things the dialog can add and everything the course branch
/// needs, and deliberately not the path: the folder is typed into the one box the dialog already
/// has, and is handed here as the command's parameter. Two boxes with one name is a defect the walk
/// refuses, and it would also mean a person retyping the path to change their mind about the kind.
/// <para>
/// The neighbours are the amendment's whole point. Pointing at one folder declares a depth for the
/// entire root, and at that depth there are usually folders nobody has said anything about — so the
/// pointed-at one is marked, the rest are counted, and «Hemos encontrado {0} carpetas más. ¿Son
/// todas cursos?» asks about the <em>fact</em> rather than the action. Answering it is a second
/// pass; walking away from it leaves exactly the one course that was asked for.
/// </para>
/// </remarks>
public sealed class MarkCourseViewModel : INotifyPropertyChanged
{
    private readonly DeclareCourseFolder? _declare;
    private readonly AsyncRelayCommand _confirm;
    private readonly AsyncRelayCommand _markNeighbours;
    private readonly AsyncRelayCommand _dismissNeighbours;
    private bool _isCourse;
    private bool _isWorking;
    private string? _failureKey;
    private string? _markedTitle;
    private IReadOnlyList<string> _neighbours = [];
    private string _lastPath = string.Empty;

    public MarkCourseViewModel(DeclareCourseFolder? declare = null)
    {
        _declare = declare;
        SelectKindCommand = new AsyncRelayCommand(
            parameter =>
            {
                // The word and not a boolean: AXAML hands a CommandParameter of "True" back as a
                // string, so a pill bound to {x:True} would select nothing and look right doing it.
                IsCourse = parameter is "course";
                return Task.CompletedTask;
            });
        _confirm = new AsyncRelayCommand(
            parameter => ConfirmAsync(parameter as string ?? string.Empty, CancellationToken.None),
            _ => !IsWorking);
        _markNeighbours = new AsyncRelayCommand(
            () => MarkNeighboursAsync(CancellationToken.None),
            () => IsAskingAboutNeighbours && !IsWorking);
        _dismissNeighbours = new AsyncRelayCommand(
            () =>
            {
                Neighbours = [];
                return Task.CompletedTask;
            },
            () => IsAskingAboutNeighbours);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Which kind of volume a root added here would sit on, as the dialog detected it from the path.
    /// The composition wires the same detector the root branch uses.
    /// </summary>
    public RootKind Kind { get; set; } = RootKind.Local;

    /// <summary>
    /// Whether the dialog is offering to mark a course rather than to add a scan root. False is the
    /// dialog's own starting state, and it goes back to false every time it opens.
    /// </summary>
    public bool IsCourse
    {
        get => _isCourse;
        private set
        {
            if (SetField(ref _isCourse, value))
            {
                OnPropertyChanged(nameof(IsRoot));
                OnPropertyChanged(nameof(RootStateCue));
                OnPropertyChanged(nameof(CourseStateCue));
                OnPropertyChanged(nameof(TitleKey));
                OnPropertyChanged(nameof(HelpKey));
                OnPropertyChanged(nameof(ConfirmKey));
                OnPropertyChanged(nameof(ShowsShape));
            }
        }
    }

    public bool IsRoot => !IsCourse;

    /// <summary>The circle this repository uses for "chosen", and the one it uses for "not".</summary>
    public string RootStateCue => IsCourse ? "○" : "●";

    public string CourseStateCue => IsCourse ? "●" : "○";

    /// <summary>
    /// What the dialog calls itself, what it explains, and what its one action says — as resource
    /// keys the surface resolves.
    /// </summary>
    /// <remarks>
    /// Keys rather than two sets of controls behind <c>IsVisible</c>, which is this tree's own
    /// precedent for text that changes with state. Two controls would also mean two buttons wearing
    /// <c>primary-action</c>, and a screen with two leading actions is a screen that has not decided
    /// what it is for — <c>LeadingActionTests</c> refuses exactly that.
    /// </remarks>
    public string TitleKey => IsCourse ? "AddCourseTitle" : "AddRootDialogTitle";

    public string HelpKey => IsCourse ? "AddCourseHelp" : "RootOnboardingDescription";

    public string ConfirmKey => IsCourse ? "AddCourseConfirmAction" : "RootAddAction";

    /// <summary>
    /// Whether to draw what a course looks like. Only while somebody is still deciding: once the
    /// folder is marked the shape has answered its question, and leaving it up pushes the notice and
    /// the neighbours' question past the bottom of a 560 px panel.
    /// </summary>
    public bool ShowsShape => IsCourse && !HasMarked;

    /// <summary>
    /// Adds the root, when the dialog is on its root half. A delegate rather than a port, which is
    /// the shape the pickers already have: the composition decides once, and this model never learns
    /// that a root has a use case behind it.
    /// </summary>
    public Func<Task>? AddRoot { get; set; }

    /// <summary>Chooses between the two things the dialog can add. The parameter is the choice.</summary>
    public ICommand SelectKindCommand { get; }

    /// <summary>
    /// The dialog's one action: adds the root, or marks the folder its parameter names and counts
    /// what sits beside it.
    /// </summary>
    public ICommand ConfirmCommand => _confirm;

    /// <summary>«Sí, son todas cursos»: a second pass that claims the neighbours as well.</summary>
    public ICommand MarkNeighboursCommand => _markNeighbours;

    /// <summary>«Sólo esta»: the question goes away and nothing further is marked.</summary>
    public ICommand DismissNeighboursCommand => _dismissNeighbours;

    /// <summary>True while a pass is running, so neither answer can be given twice.</summary>
    public bool IsWorking
    {
        get => _isWorking;
        private set
        {
            // Set and told, with no guard on whether it changed: it is only ever written on the two
            // edges of a pass, so a guard would be a branch that cannot be taken.
            SetField(ref _isWorking, value);
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>The folders found at the same depth and left alone, pending an answer.</summary>
    public IReadOnlyList<string> Neighbours
    {
        get => _neighbours;
        private set
        {
            _neighbours = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NeighbourCount));
            OnPropertyChanged(nameof(IsAskingAboutNeighbours));
            RaiseCanExecuteChanged();
        }
    }

    public int NeighbourCount => Neighbours.Count;

    /// <summary>
    /// Whether the question is on screen. Nothing found beside the course means nothing to ask, and
    /// asking about zero folders would be a dialog demanding an answer it already has.
    /// </summary>
    public bool IsAskingAboutNeighbours => Neighbours.Count > 0;

    /// <summary>The course that was just marked, or null when none has been.</summary>
    public string? MarkedTitle
    {
        get => _markedTitle;
        private set
        {
            if (SetField(ref _markedTitle, value))
            {
                OnPropertyChanged(nameof(HasMarked));
                OnPropertyChanged(nameof(ShowsShape));
            }
        }
    }

    public bool HasMarked => MarkedTitle is not null;

    /// <summary>Which refusal is on screen, as a resource key, or nothing when the last pass worked.</summary>
    public string? FailureKey
    {
        get => _failureKey;
        private set
        {
            if (SetField(ref _failureKey, value))
            {
                OnPropertyChanged(nameof(HasFailure));
            }
        }
    }

    public bool HasFailure => FailureKey is not null;

    /// <summary>Puts the course half back to nothing, so the next folder starts on a clean dialog.</summary>
    public void Begin()
    {
        IsCourse = false;
        Neighbours = [];
        MarkedTitle = null;
        FailureKey = null;
    }

    /// <summary>
    /// Marks <paramref name="path"/> as a course and remembers what else sits at its depth.
    /// </summary>
    /// <remarks>
    /// A refusal is answered on the screen rather than thrown, for the reason the root branch already
    /// learned: letting it out of here reaches the dispatcher, and on Windows that ends the process.
    /// </remarks>
    /// <summary>
    /// The dialog's one action: adds the root, or marks <paramref name="path"/> as a course.
    /// </summary>
    public Task ConfirmAsync(string path, CancellationToken cancellationToken = default) =>
        IsCourse
            ? MarkAsync(path, cancellationToken)
            : AddRoot?.Invoke() ?? Task.CompletedTask;

    public Task MarkAsync(string path, CancellationToken cancellationToken = default) =>
        RunAsync(path, alsoMark: null, cancellationToken);

    /// <summary>
    /// Marks the neighbours as courses too, which is what a yes to the question means.
    /// </summary>
    /// <remarks>
    /// It re-reads the root instead of trusting what the first pass counted. That costs one more
    /// walk of a folder somebody is standing in front of, and it buys an answer that is true when it
    /// is acted on rather than true when it was computed.
    /// </remarks>
    public Task MarkNeighboursAsync(CancellationToken cancellationToken = default) =>
        Neighbours.Count == 0
            ? Task.CompletedTask
            : RunAsync(_lastPath, Neighbours, cancellationToken);

    /// <summary>
    /// One pass, whichever of the two asked for it, so there is one place a refusal is turned into
    /// a sentence rather than two that could disagree.
    /// </summary>
    private async Task RunAsync(
        string path,
        IReadOnlyCollection<string>? alsoMark,
        CancellationToken cancellationToken)
    {
        if (_declare is null || IsWorking)
        {
            return;
        }

        IsWorking = true;
        try
        {
            var declared = await _declare
                .ExecuteAsync(new DeclareCourseFolderCommand(path, Kind, alsoMark), cancellationToken)
                .ConfigureAwait(true);
            _lastPath = path;
            Neighbours = declared.Others;

            // A pass that marked nothing found no video deep enough to be a lesson, and saying so is
            // the whole answer: the folder is real, it is just not a course yet.
            MarkedTitle = declared.Marked.Count > 0 ? declared.Marked[0].Title : MarkedTitle;
            FailureKey = declared.Marked.Count > 0 ? null : "AddCourseNoVideoFound";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MarkedTitle = null;
            Neighbours = [];
            FailureKey = Describe(exception);
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// <summary>
    /// Turns a refusal into the sentence the dialog shows. Two cases and not three: a folder no
    /// course can be declared from, and a parent that would nest one root inside another.
    /// </summary>
    /// <remarks>
    /// There is deliberately no duplicate branch — a parent that is already a root is found by the
    /// derivation before anything is added, so <c>AddLibraryRoot</c> never sees one from this door.
    /// And the word alone decides, without asking the exception's type first: the only other one
    /// that reaches here is the <c>ArgumentException</c> naming a path, and no path is called
    /// "Nested". A type test would be a second condition with a side nothing can take.
    /// </remarks>
    private static string Describe(Exception exception) =>
        exception.Message.Contains("Nested", StringComparison.Ordinal)
            ? "RootAddNested"
            : "AddCourseInvalidFolder";

    /// <summary>
    /// Tells the three buttons to ask again.
    /// </summary>
    /// <remarks>
    /// A button bound to a command asks <c>CanExecute</c> once and then waits to be told. Without
    /// this, «Marcar todas» is created while there are no neighbours, answers false, and stays
    /// disabled for the whole life of the dialog — on screen, correct-looking, and unpressable. The
    /// autonomous walk is what found it: no unit test would have, because <c>CanExecute</c> read
    /// straight off the model gives the right answer every time.
    /// </remarks>
    private void RaiseCanExecuteChanged()
    {
        _confirm.RaiseCanExecuteChanged();
        _markNeighbours.RaiseCanExecuteChanged();
        _dismissNeighbours.RaiseCanExecuteChanged();
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
