// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Shell;

/// <summary>
/// Which of the session's side panels is standing open, if any.
/// </summary>
/// <remarks>
/// The prototype heads the player with four pills and gives the column to whichever one is pressed;
/// pressing the same pill again gives the width back to the picture. That is why <see cref="None"/>
/// is a value here rather than a nullable around the enum: the closed column is a state the header
/// can show, not the absence of one.
/// </remarks>
public enum PlayerPanel
{
    /// <summary>No panel: the picture has the whole width.</summary>
    None = 0,

    /// <summary>Audio tracks, the output device, and the channel layout.</summary>
    Audio,

    /// <summary>Subtitle tracks and what is loaded beside the media.</summary>
    Subtitles,

    /// <summary>What the decoder and the display agreed on.</summary>
    Video,

    /// <summary>Detected ranges and the ones this title keeps.</summary>
    Markers,

    /// <summary>The other versions of what is playing.</summary>
    Versions,
}
