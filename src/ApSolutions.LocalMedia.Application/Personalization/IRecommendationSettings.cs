// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Personalization;

/// <summary>
/// Whether the person wants suggestions at all. The answer is remembered between sessions, which is
/// why it is a stored setting rather than a field on a view.
/// </summary>
public interface IRecommendationSettings
{
    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);
}
