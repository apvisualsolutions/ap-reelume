// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Application.Personalization;

/// <summary>
/// Whether the person wants suggestions at all. The answer is remembered between sessions, which is
/// why it is a stored setting rather than a field on a view.
/// </summary>
public interface IRecommendationSettings
{
    /// <summary>
    /// On until somebody turns it off, and what UX-010's reset puts back. Named on the port so the
    /// store and the reset cannot disagree about which of them holds the default.
    /// </summary>
    const bool EnabledByDefault = true;

    bool IsEnabled { get; }

    void SetEnabled(bool isEnabled);
}
