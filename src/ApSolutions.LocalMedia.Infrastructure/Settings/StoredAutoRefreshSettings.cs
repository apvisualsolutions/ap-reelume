// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Settings;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Remembers whether the application may refresh stored metadata nobody asked it to refresh.
/// </summary>
/// <remarks>
/// The absence of the setting means no, exactly as it does for automatic update checks: an
/// installation that has never been asked has not answered, and a connection is not something to
/// make on the strength of a missing value.
/// </remarks>
public sealed class StoredAutoRefreshSettings : IAutoRefreshSettings
{
    private const string Key = "metadata.automaticRefreshEnabled";

    private readonly ISettingsStore _store;

    public StoredAutoRefreshSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool AutomaticRefreshEnabled => _store.Read<bool?>(Key) ?? false;

    public void SetAutomaticRefreshEnabled(bool enabled) => _store.Write(Key, enabled);
}
