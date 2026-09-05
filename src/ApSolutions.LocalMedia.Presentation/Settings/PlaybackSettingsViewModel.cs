// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApSolutions.LocalMedia.Presentation.Settings;

/// <summary>
/// The Playback section of Settings: how long the next-episode countdown waits, and whether it waits
/// at all.
/// </summary>
/// <remarks>
/// <para>
/// <b>This surface is what PLY-011 promised and did not have.</b> Its criterion says the countdown
/// is «cancelable, configurable», and the length has been stored in a preference since T28 — read at
/// playback, with zero switching the whole chain off — while the only thing that ever wrote it was
/// the tests. <c>ContinuityCountdown</c>'s own comment claimed «the settings surface already reads
/// and writes» that key. It did not exist.
/// </para>
/// <para>
/// <b>Two rows for one preference, and the reason is that they answer different questions.</b> The
/// prototype draws a toggle, which asks «does the next thing start on its own?»; the store keeps
/// seconds from zero to sixty, which asks «how long do I have to say no?». A toggle alone would drop
/// the length — off writes zero, on returns to ten, and somebody who wants thirty seconds has
/// nowhere to ask. A slider alone answers the second question and leaves the first to be inferred
/// from a zero. So the toggle owns the on/off and the slider appears under it, which is the shape
/// the owner chose on 2026-09-05 with both drawn side by side.
/// </para>
/// <para>
/// <b>The slider never writes zero.</b> Zero is the toggle's word, and a slider that could reach it
/// would give two controls the same say over one value: dragging to the left edge would silently
/// switch the chain off while the toggle above still read «on». The slider's floor is therefore the
/// smallest wait the chain can honour, and turning the toggle back on restores the length that was
/// in force rather than the default — otherwise switching off and on again would quietly discard a
/// chosen thirty seconds.
/// </para>
/// </remarks>
public sealed class PlaybackSettingsViewModel : INotifyPropertyChanged
{
    /// <summary>The shortest wait the slider offers; zero belongs to the toggle alone.</summary>
    public const int MinimumSeconds = 5;

    /// <summary>The longest wait the store accepts, mirrored here for the slider's binding.</summary>
    public const int MaximumSeconds = 60;

    /// <summary>
    /// The slider's own bounds. A <c>RangeBase</c> takes doubles, and binding an int to Minimum
    /// refuses at compile time rather than at run time - which is the compiler earning its keep.
    /// </summary>
    public static double MinimumCountdownSeconds => MinimumSeconds;

    /// <inheritdoc cref="MinimumCountdownSeconds"/>
    public static double MaximumCountdownSeconds => MaximumSeconds;

    /// <summary>What the toggle restores when the stored length is zero.</summary>
    public const int DefaultCountdownSeconds = 10;

    private readonly Func<int> _readCountdownSeconds;
    private readonly Action<int> _writeCountdownSeconds;

    /// <summary>
    /// The length to restore when the countdown is switched back on, held only while it is off.
    /// </summary>
    private int _lengthBeforeSwitchingOff;

    public PlaybackSettingsViewModel(Func<int> readCountdownSeconds, Action<int> writeCountdownSeconds)
    {
        _readCountdownSeconds = readCountdownSeconds ?? throw new ArgumentNullException(nameof(readCountdownSeconds));
        _writeCountdownSeconds = writeCountdownSeconds ?? throw new ArgumentNullException(nameof(writeCountdownSeconds));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Whether the next thing starts on its own; false is the stored zero.</summary>
    public bool IsCountdownEnabled
    {
        get => _readCountdownSeconds() > 0;
        set
        {
            if (IsCountdownEnabled == value)
            {
                return;
            }

            if (value)
            {
                var restored = _lengthBeforeSwitchingOff > 0 ? _lengthBeforeSwitchingOff : DefaultCountdownSeconds;
                _writeCountdownSeconds(Clamp(restored));
            }
            else
            {
                _lengthBeforeSwitchingOff = _readCountdownSeconds();
                _writeCountdownSeconds(0);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CountdownSeconds));
        }
    }

    /// <summary>
    /// How long the countdown waits, in seconds. Reads the floor while the countdown is off, so the
    /// slider never shows a zero it cannot produce.
    /// </summary>
    public int CountdownSeconds
    {
        get
        {
            var stored = _readCountdownSeconds();
            return stored > 0 ? Clamp(stored) : Clamp(_lengthBeforeSwitchingOff);
        }

        set
        {
            var wanted = Clamp(value);
            if (!IsCountdownEnabled)
            {
                // The slider is hidden while the countdown is off, so a write here can only come
                // from a binding settling. Remember it without switching the chain back on.
                _lengthBeforeSwitchingOff = wanted;
                OnPropertyChanged();
                return;
            }

            if (_readCountdownSeconds() == wanted)
            {
                return;
            }

            _writeCountdownSeconds(wanted);
            OnPropertyChanged();
        }
    }

    private static int Clamp(int seconds) =>
        Math.Clamp(seconds <= 0 ? DefaultCountdownSeconds : seconds, MinimumSeconds, MaximumSeconds);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
