// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Application.Settings;

public interface ISettingsStore
{
    T? Read<T>(string key);

    void Write<T>(string key, T value);
}
