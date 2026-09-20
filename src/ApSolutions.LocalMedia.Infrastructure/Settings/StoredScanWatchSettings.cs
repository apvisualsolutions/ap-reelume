// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Discovery;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>Remembers whether local folders are followed live.</summary>
/// <remarks>
/// <b>The absence of the setting means yes here, which is the opposite of its neighbours, and the
/// difference is the network.</b> Automatic refresh and update checks default to no because an
/// installation that was never asked has not consented to reaching outside the machine. Watching a
/// folder reaches nothing: it reads the disk the person already pointed at and asked to have
/// catalogued. The scope record promises local changes apply within seconds (LIB-003), so an
/// installation nobody has configured has to see a new film appear.
/// </remarks>
public sealed class StoredScanWatchSettings : IScanWatchSettings
{
    private const string Key = "scan.watchLocalRoots";

    /// <summary>Stored in whole minutes because that is what the screen edits.</summary>
    private const string SweepKey = "scan.sweepIntervalMinutes";

    private readonly ISettingsStore _store;

    public StoredScanWatchSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public event EventHandler? Changed;

    public bool WatchLocalRoots => _store.Read<bool?>(Key) ?? true;

    /// <summary>
    /// <b>The clamping lives here and not in the scheduler, on purpose.</b> This is the only place
    /// that reads a file a person can open in a text editor, so it is where a value out of range can
    /// arrive — a sweep every zero minutes is a scan in a hot loop. Doing it downstream instead would
    /// force every test of the scheduler to wait a whole minute for a pass.
    /// </summary>
    public TimeSpan SweepInterval
    {
        get
        {
            var stored = _store.Read<int?>(SweepKey);
            if (stored is not { } minutes)
            {
                return ScanWatchPolicy.DefaultSweepInterval;
            }

            var asked = TimeSpan.FromMinutes(minutes);
            if (asked < ScanWatchPolicy.MinimumSweepInterval)
            {
                return ScanWatchPolicy.MinimumSweepInterval;
            }

            return asked > ScanWatchPolicy.MaximumSweepInterval
                ? ScanWatchPolicy.MaximumSweepInterval
                : asked;
        }
    }

    public void SetWatchLocalRoots(bool enabled)
    {
        _store.Write(Key, enabled);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetSweepInterval(TimeSpan interval)
    {
        _store.Write(SweepKey, (int)Math.Round(interval.TotalMinutes));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
