// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.Application.Settings;

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

    private readonly ISettingsStore _store;

    public StoredScanWatchSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool WatchLocalRoots => _store.Read<bool?>(Key) ?? true;

    public void SetWatchLocalRoots(bool enabled) => _store.Write(Key, enabled);
}
