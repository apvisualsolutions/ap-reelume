// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Application.Settings;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Keeps the mini player where it was left, across sessions.
/// </summary>
/// <remarks>
/// A stored placement that cannot be used is answered as none rather than handed on: the settings
/// file is text on somebody's disk, and a hand-edited zero width or a NaN would put the window at a
/// size no screen can show it at. Repairing on the way out is what every other stored preference in
/// this application does, and for the same reason.
/// </remarks>
public sealed class StoredMiniPlayerPlacement : IMiniPlayerPlacementStore
{
    private const string PlacementKey = "player.miniPlacement";

    private readonly ISettingsStore _store;

    public StoredMiniPlayerPlacement(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public MiniPlayerPlacement? Read()
    {
        var stored = _store.Read<MiniPlayerPlacement?>(PlacementKey);
        return stored is not null && IsUsable(stored) ? stored : null;
    }

    public void Save(MiniPlayerPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        // A placement that would not be read back is not written: the file would then hold a value
        // this class already knows it will refuse, which is a bug that only shows up next launch.
        if (IsUsable(placement))
        {
            _store.Write(PlacementKey, placement);
        }
    }

    private static bool IsUsable(MiniPlayerPlacement placement) =>
        double.IsFinite(placement.X)
        && double.IsFinite(placement.Y)
        && placement.Width > 0
        && placement.Height > 0
        && double.IsFinite(placement.Width)
        && double.IsFinite(placement.Height);
}
