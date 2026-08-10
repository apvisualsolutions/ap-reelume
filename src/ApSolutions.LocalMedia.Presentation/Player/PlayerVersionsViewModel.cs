// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Catalog;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// One other version of the content that is playing, and the action that moves the session to it.
/// It states resolution, codec and range, which is what tells versions apart — never the path.
/// </summary>
public sealed class PlayerVersionRowViewModel
{
    private readonly MediaVersion _version;

    public PlayerVersionRowViewModel(MediaVersion version, Func<MediaVersion, Task> onSwitch)
    {
        _version = version ?? throw new ArgumentNullException(nameof(version));
        ArgumentNullException.ThrowIfNull(onSwitch);
        SwitchCommand = new SwitchRowCommand(() => onSwitch(_version), () => _version.IsAvailable);
    }

    public MediaVersion Version => _version;

    public bool IsAvailable => _version.IsAvailable;

    public ICommand SwitchCommand { get; }

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

    private sealed class SwitchRowCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
    {
        private bool _isRunning;

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => !_isRunning && canExecute();

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter))
            {
                return;
            }

            _isRunning = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            try
            {
                await execute().ConfigureAwait(true);
            }
            finally
            {
                _isRunning = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
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
