// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

using Avalonia.Controls;

namespace ApSolutions.LocalMedia.Presentation.Theme;

public interface IBackdropService
{
    bool TryApply(Window window);
}
