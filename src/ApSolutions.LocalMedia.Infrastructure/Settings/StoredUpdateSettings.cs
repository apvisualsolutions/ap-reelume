// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Application.Updates;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Remembers whether the application may look for updates nobody asked it to look for.
/// </summary>
/// <remarks>
/// The absence of the setting means no, as everywhere else here. An installation that has never been
/// asked the question has not answered it, and a connection is not something to make on the strength
/// of a missing value.
/// </remarks>
public sealed class StoredUpdateSettings : IUpdateSettings
{
    private const string AutomaticKey = "updates.automaticCheckEnabled";

    private readonly ISettingsStore _store;

    public StoredUpdateSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool AutomaticCheckEnabled => _store.Read<bool?>(AutomaticKey) ?? false;

    public void SetAutomaticCheckEnabled(bool enabled) => _store.Write(AutomaticKey, enabled);
}
