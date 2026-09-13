// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-AP-Reelume

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Which group of options the gear is showing, if any (ADR-0012).
/// </summary>
/// <remarks>
/// <see cref="None"/> is the list itself rather than the absence of a group, the same way
/// <c>PlayerPanel.None</c> is the closed column: the first level is a state the gear can be in, and
/// a nullable around this would make «showing the list» and «showing nothing» the same value.
/// </remarks>
public enum PlayerSettingsGroup
{
    /// <summary>The first level: the list of groups.</summary>
    None = 0,

    /// <summary>Brightness, contrast and gamma (PLY-018).</summary>
    Picture,

    /// <summary>
    /// How long the next episode waits before it starts on its own. It came down from Settings on
    /// 2026-09-13 (ADR-0012): the number somebody wants is the one they arrive at having just sat
    /// through an episode, which is not a question anybody answers on a settings page.
    /// </summary>
    NextEpisode,

    /// <summary>
    /// Whether intros are detected and offered for skipping, down from Settings on the same day and
    /// for the same reason: it is judged the moment one is, or is not, skipped.
    /// </summary>
    Segments,

    /// <summary>
    /// How the subtitles look: size, family, the two colours, the background's opacity and the
    /// outline. Down from Settings on 2026-09-13 as well, and the last of the three: the right font
    /// size is the one that reads over THIS film at this distance, which nobody can answer without
    /// something playing behind the text.
    /// </summary>
    Subtitles,
}
