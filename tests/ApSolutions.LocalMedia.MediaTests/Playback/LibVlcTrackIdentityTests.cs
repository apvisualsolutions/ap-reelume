// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Infrastructure.Playback;
using Xunit;

namespace ApSolutions.LocalMedia.MediaTests.Playback;

/// <summary>
/// How LibVLC says «this kind is switched off», and how it names the track when it is not.
/// </summary>
/// <remarks>
/// It lives outside the adapter so that it can be measured anywhere: the adapter needs a decoder,
/// and a rule that is only covered on a machine with one is a rule that falls below its floor on a
/// hosted runner. This one is arithmetic.
/// </remarks>
public sealed class LibVlcTrackIdentityTests
{
    [Fact]
    public void The_disabled_sentinel_is_no_track_rather_than_a_track_called_minus_one() =>
        Assert.Null(LibVlcTrackIdentity.Announced(LibVlcTrackIdentity.Disabled));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void Any_other_identifier_is_announced_as_the_number_it_is(int identifier) =>
        Assert.Equal(
            identifier.ToString(CultureInfo.InvariantCulture),
            LibVlcTrackIdentity.Announced(identifier));

    /// <summary>
    /// Invariant, because the identifier is parsed back the same way when a track is selected.
    /// </summary>
    [Fact]
    public void The_identifier_is_written_the_same_way_whatever_the_machine_speaks()
    {
        var before = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("123456", LibVlcTrackIdentity.Announced(123456));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}
