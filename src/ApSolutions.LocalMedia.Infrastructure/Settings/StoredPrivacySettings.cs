// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Privacy;
using ApSolutions.LocalMedia.Application.Settings;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Remembers whether diagnostics were allowed, and since when.
/// <para>
/// The absence of the setting means no. A stored consent whose date is missing is treated as no consent
/// at all rather than as consent of unknown age: a permission nobody can date is a permission nobody
/// gave.
/// </para>
/// </summary>
public sealed class StoredPrivacySettings : IPrivacySettings
{
    private const string GrantedKey = "privacy.diagnosticsEnabled";
    private const string GrantedUtcKey = "privacy.diagnosticsGrantedUtc";

    private readonly ISettingsStore _store;

    public StoredPrivacySettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public DiagnosticsConsent Current
    {
        get
        {
            var granted = _store.Read<bool?>(GrantedKey) ?? false;
            var when = _store.Read<DateTimeOffset?>(GrantedUtcKey);
            return granted && when is not null
                ? new DiagnosticsConsent(true, when)
                : new DiagnosticsConsent(false, null);
        }
    }

    public void Save(DiagnosticsConsent consent)
    {
        ArgumentNullException.ThrowIfNull(consent);
        var granted = consent.IsGranted && consent.GrantedUtc is not null;
        _store.Write(GrantedKey, granted);
        _store.Write(GrantedUtcKey, granted ? consent.GrantedUtc!.Value : DateTimeOffset.MinValue);
    }
}
