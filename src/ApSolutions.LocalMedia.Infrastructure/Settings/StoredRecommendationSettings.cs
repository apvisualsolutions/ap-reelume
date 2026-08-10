// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Personalization;
using ApSolutions.LocalMedia.Application.Settings;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Remembers whether recommendations are wanted. The default is on; turning them off persists, and a
/// stored "off" is honoured on the next start without the computation ever running.
/// </summary>
public sealed class StoredRecommendationSettings : IRecommendationSettings
{
    private const string Key = "recommendations.enabled";

    private readonly ISettingsStore _store;

    public StoredRecommendationSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool IsEnabled => _store.Read<bool?>(Key) ?? true;

    public void SetEnabled(bool isEnabled) => _store.Write(Key, isEnabled);
}
