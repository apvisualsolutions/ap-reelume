// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using System.ComponentModel;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The badge names the film and the line under it names the path, so the two are separate flags.
/// </summary>
public sealed class VideoStatusViewModelTests
{
    [Fact]
    public void An_HDR10_film_brought_down_is_an_HDR_source_on_the_tone_mapped_path()
    {
        var status = new VideoStatusViewModel();
        var changed = new List<string?>();
        status.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        status.Apply(new PlaybackCapabilities(false, false, HdrFormat.Hdr10, true, VideoOutputPath.SdrToneMapped), false);

        Assert.True(status.IsHdrSource);
        Assert.True(status.IsToneMapped);
        Assert.False(status.IsHdrPassthrough);
        Assert.False(status.IsStandardDynamicRange);
        Assert.Contains(nameof(VideoStatusViewModel.IsHdrSource), changed);
    }

    [Fact]
    public void A_standard_film_and_no_film_at_all_are_not_HDR_sources()
    {
        var status = new VideoStatusViewModel();

        // Nobody listening, which is how the status is built before the view binds to it.
        status.Apply(new PlaybackCapabilities(false, false), false);
        Assert.False(status.IsHdrSource);
        Assert.True(status.IsStandardDynamicRange);

        status.Apply(null, false);
        Assert.False(status.IsHdrSource);
        Assert.False(status.HasStatus);
    }
}
