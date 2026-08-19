// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Catalog;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// One other version of the content that is playing, and the action that moves the session to it.
/// It states resolution, codec and range, which is what tells versions apart — never the path.
/// </summary>
public sealed class PlayerVersionRowViewModel
{
    private readonly MediaVersion _version;
    private readonly VersionSwitchViewModel _question;
    private readonly AsyncRelayCommand _switch;
    private bool _isSwitching;

    /// <summary>
    /// The row, the question its switch can raise, and the switch itself. The question is mandatory
    /// because a row that does not know about it is pressable underneath it, and an optional one
    /// left at null would compile into exactly that.
    /// </summary>
    public PlayerVersionRowViewModel(
        MediaVersion version,
        VersionSwitchViewModel question,
        Func<MediaVersion, Task> onSwitch)
    {
        _version = version ?? throw new ArgumentNullException(nameof(version));
        _question = question ?? throw new ArgumentNullException(nameof(question));
        ArgumentNullException.ThrowIfNull(onSwitch);
        _switch = new AsyncRelayCommand(
            () => SwitchAsync(onSwitch),
            () => _version.IsAvailable && !_isSwitching && !_question.IsVisible);

        // The dialogue and the rows are built together and replaced together, so this needs no
        // unsubscribing: the question never outlives the row that listens to it.
        _question.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(VersionSwitchViewModel.IsVisible))
            {
                _switch.RaiseCanExecuteChanged();
            }
        };
    }

    public MediaVersion Version => _version;

    public bool IsAvailable => _version.IsAvailable;

    public ICommand SwitchCommand => _switch;

    /// <summary>
    /// The switch, with the row unpressable while its own work is in flight. The transport bar
    /// already does this — a skip is disabled while the previous one seeks — and this row needed it
    /// for a sharper reason: every switch flushes the playhead before it decides what to do with the
    /// progress, and a session whose demuxer has not applied its start position yet answers zero.
    /// Zero is below the resume floor, so a second switch decides there is nothing to carry across,
    /// opens the other version without asking and writes the stored position away. Measured on CI on
    /// 2026-08-18, where the harness pressed again after 300 ms of apparent silence and the question
    /// it was about to answer vanished from the screen underneath it.
    /// </summary>
    private async Task SwitchAsync(Func<MediaVersion, Task> onSwitch)
    {
        _isSwitching = true;
        _switch.RaiseCanExecuteChanged();
        try
        {
            await onSwitch(_version).ConfigureAwait(true);
        }
        finally
        {
            // Pressable again even when the switch failed, or one refusal would leave a dead row and
            // the failure would read as a button that does nothing.
            _isSwitching = false;
            _switch.RaiseCanExecuteChanged();
        }
    }

    public string QualityLabel => string.Join(
        " · ",
        new[]
        {
            _version is { Width: { } width, Height: { } height }
                ? string.Create(CultureInfo.CurrentCulture, $"{width}×{height}")
                : null,
            string.IsNullOrWhiteSpace(_version.VideoCodec) ? null : _version.VideoCodec,
            _version.IsHdr ? "HDR" : null,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));
}

/// <summary>
/// The other versions of what is playing (VSW-A01). The surface only exists when the title has a
/// version group, and each row hands its switch to the use case that carries progress across.
/// </summary>
public sealed class PlayerVersionsViewModel
{
    public PlayerVersionsViewModel(IReadOnlyList<PlayerVersionRowViewModel> alternatives) =>
        Alternatives = alternatives ?? throw new ArgumentNullException(nameof(alternatives));

    public IReadOnlyList<PlayerVersionRowViewModel> Alternatives { get; }

    public bool HasAlternatives => Alternatives.Count > 0;
}
