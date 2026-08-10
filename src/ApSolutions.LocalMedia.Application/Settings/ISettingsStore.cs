// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Settings;

public interface ISettingsStore
{
    T? Read<T>(string key);

    void Write<T>(string key, T value);
}
