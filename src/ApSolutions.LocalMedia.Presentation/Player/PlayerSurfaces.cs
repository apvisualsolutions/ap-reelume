// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Everything one playing session puts on screen, handed over together.
/// <para>
/// They travel as one object because they belong to one session: the tracks, the output device, the
/// markers, and the resume offer all describe the same media, and letting the shell assemble them
/// one by one is how a player ends up with a track list from the previous file.
/// </para>
/// </summary>
public sealed record PlayerSurfaces
{
    public required PlayerViewModel Player { get; init; }

    /// <summary>
    /// What is playing, as the surface that started it calls it, and the line under it.
    /// </summary>
    /// <remarks>
    /// The header's middle was empty until 2026-08-25 with the reason written into the view: the
    /// session holds a path and nothing else, and a file path is not a heading. It is not looked up
    /// here either — it travels with the request, from the card that pressed Play.
    /// </remarks>
    public string? Title { get; init; }

    public string? Subtitle { get; init; }

    /// <summary>Audio and subtitle tracks of the media being played.</summary>
    public TrackSelectorViewModel? Tracks { get; init; }

    /// <summary>The endpoint the sound is going to, and what it could not do.</summary>
    public AudioOutputViewModel? AudioOutput { get; init; }

    /// <summary>Manual intro, recap, and credits ranges for the series this episode belongs to.</summary>
    public MarkerEditorViewModel? Markers { get; init; }

    /// <summary>What automatic detection found in this episode, ready to accept, adjust, or remove.</summary>
    public DetectedMarkerReviewViewModel? DetectedReview { get; init; }

    /// <summary>
    /// The stored position offer; absent when there is nothing worth returning to, and absent when
    /// whoever opened the session already said where it starts.
    /// </summary>
    public ResumePromptViewModel? Resume { get; init; }

    /// <summary>The skip offer while the playhead is inside a marked range.</summary>
    public SkipMarkerViewModel? Skip { get; init; }

    public NextEpisodeViewModel? NextEpisode { get; init; }

    /// <summary>The question asked when progress moves between two versions of the same content.</summary>
    public VersionSwitchViewModel? VersionSwitch { get; init; }

    /// <summary>The other versions of what is playing; absent when the title has no group.</summary>
    public PlayerVersionsViewModel? Versions { get; init; }

    /// <summary>What the decoder and the display actually agreed on.</summary>
    public VideoStatusViewModel? VideoStatus { get; init; }

    /// <summary>Present only when this session came from a file outside the library.</summary>
    public LooseFileViewModel? LooseFile { get; init; }
}
