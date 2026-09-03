// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Presentation.Metadata;

/// <summary>
/// Choosing the artwork for a title: a file of your own, or the one the provider offered.
/// </summary>
/// <remarks>
/// <b>The personal half had no way in until 2026-09-03.</b> <see cref="SelectedPersonalPath"/> was
/// declared, read by <see cref="CanApply"/> and written by nothing — measured across the whole tree.
/// The command below is what writes it, and the two functions it needs are handed in rather than
/// reached for: opening a file dialog belongs to the host, and importing belongs to a use case.
/// <para>
/// Both are optional so that the surfaces which only display a picker still build one with no
/// arguments. A picker built that way <b>says so</b> — the command refuses to execute — rather than
/// accepting a press and doing nothing, which is how a button ends up looking broken to the one
/// person who tries it.
/// </para>
/// </remarks>
public sealed class ArtworkPickerViewModel : INotifyPropertyChanged
{
    private readonly Func<CancellationToken, Task<string?>>? _chooseFile;
    private readonly Func<TitleId, string, string, CancellationToken, Task<PersonalCoverResult>>? _import;
    private string? _selectedPersonalPath;
    private Uri? _selectedRemoteUri;
    private string? _alternativeText;
    private string? _status;
    private bool _isChoosing;
    private TitleId? _target;

    public ArtworkPickerViewModel(
        Func<CancellationToken, Task<string?>>? chooseFile = null,
        Func<TitleId, string, string, CancellationToken, Task<PersonalCoverResult>>? import = null)
    {
        _chooseFile = chooseFile;
        _import = import;
        ChooseCoverCommand = new PickerCommand(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The title a chosen cover belongs to; the editor sets it as it loads one.</summary>
    /// <remarks>
    /// Setting it can make the button pressable, so it says so: a target arriving after the surface
    /// was drawn would otherwise leave the button disabled until something else happened to change.
    /// </remarks>
    public TitleId? Target
    {
        get => _target;
        set
        {
            _target = value;
            OnPropertyChanged(nameof(CanChoose));
            ((PickerCommand)ChooseCoverCommand).Raise();
        }
    }

    /// <summary>Opens the system picker, checks what came back, and imports it.</summary>
    public ICommand ChooseCoverCommand { get; }

    /// <summary>
    /// What happened to the last attempt, in words, or nothing before there has been one.
    /// </summary>
    /// <remarks>
    /// Every outcome says something, refusals included. A cover that did not change with no
    /// explanation is the failure this whole feature is most likely to produce.
    /// </remarks>
    public string? Status
    {
        get => _status;
        private set
        {
            if (SetField(ref _status, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    /// <summary>True while the dialog is open or the file is being copied.</summary>
    public bool IsChoosing
    {
        get => _isChoosing;
        private set
        {
            if (SetField(ref _isChoosing, value))
            {
                OnPropertyChanged(nameof(CanChoose));
                ((PickerCommand)ChooseCoverCommand).Raise();
            }
        }
    }

    /// <summary>Whether this picker was built with everything it needs to choose a cover.</summary>
    public bool CanChoose => _chooseFile is not null && _import is not null && Target is not null && !IsChoosing;

    public string? SelectedPersonalPath
    {
        get => _selectedPersonalPath;
        set
        {
            if (SetField(ref _selectedPersonalPath, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public Uri? SelectedRemoteUri
    {
        get => _selectedRemoteUri;
        set
        {
            if (SetField(ref _selectedRemoteUri, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public string? AlternativeText
    {
        get => _alternativeText;
        set
        {
            if (SetField(ref _alternativeText, value))
            {
                OnPropertyChanged(nameof(CanApply));
            }
        }
    }

    public bool CanApply =>
        !string.IsNullOrWhiteSpace(AlternativeText)
        && (!string.IsNullOrWhiteSpace(SelectedPersonalPath) || SelectedRemoteUri is not null);

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

    private async Task ChooseAsync()
    {
        if (_chooseFile is null || _import is null || Target is not { } target)
        {
            return;
        }

        IsChoosing = true;
        try
        {
            var chosen = await _chooseFile(CancellationToken.None).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(chosen))
            {
                // A cancelled dialog is not a refusal and says nothing: somebody who changed their
                // mind does not need to be told they did.
                Status = null;
                return;
            }

            // The alternative text is required by the store and is the file's own name until
            // somebody writes a better one — an empty one would be refused, and refusing a cover
            // over a field the person has not reached yet is a dead end.
            var describedAs = string.IsNullOrWhiteSpace(AlternativeText)
                ? Path.GetFileNameWithoutExtension(chosen)
                : AlternativeText;

            var result = await _import(target, chosen, describedAs, CancellationToken.None)
                .ConfigureAwait(true);

            Status = PresentationText.Resource(StatusKey(result.Verdict), Fallback(result.Verdict));
            if (result.Succeeded)
            {
                AlternativeText = describedAs;
                SelectedPersonalPath = result.Path;
            }
        }
        finally
        {
            IsChoosing = false;
        }
    }

    private static string StatusKey(CoverImageVerdict verdict) => verdict switch
    {
        CoverImageVerdict.Approved => "CoverChosenStatus",
        CoverImageVerdict.NotAnApprovedImage => "CoverNotAnImageStatus",
        CoverImageVerdict.TooLarge => "CoverTooLargeStatus",
        CoverImageVerdict.Empty => "CoverEmptyStatus",
        _ => "CoverNothingChosenStatus",
    };

    private static string Fallback(CoverImageVerdict verdict) => verdict switch
    {
        CoverImageVerdict.Approved => "Cover set.",
        CoverImageVerdict.NotAnApprovedImage => "That file is not one of the image kinds this takes.",
        CoverImageVerdict.TooLarge => "That image is larger than a cover is allowed to be.",
        CoverImageVerdict.Empty => "That file is empty.",
        _ => "No file was chosen.",
    };

    /// <summary>
    /// The command, written here rather than reached for from a toolkit because this assembly has
    /// no async relay of its own on this path and one command does not earn a dependency.
    /// </summary>
    /// <remarks>
    /// <b>The owner raises the change rather than the command subscribing to it.</b> The first
    /// version added a handler to <c>PropertyChanged</c> inside the event's own <c>add</c> and left
    /// <c>remove</c> empty — a subscription per listener that nothing could ever release, and the
    /// owner outlives the command's listeners. Coverage is what pointed at it: two branches nobody
    /// could reach, in four lines that should not have had any.
    /// </remarks>
    private sealed class PickerCommand(ArtworkPickerViewModel owner) : ICommand
    {
        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => owner.CanChoose;

        public async void Execute(object? parameter) => await owner.ChooseAsync().ConfigureAwait(true);

        public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
