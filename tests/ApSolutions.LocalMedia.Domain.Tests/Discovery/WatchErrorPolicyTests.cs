// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Domain.Discovery;
using Xunit;

namespace ApSolutions.LocalMedia.Domain.Tests.Discovery;

/// <summary>
/// BUG-012. The overflow that raised this arrived as an intermittent red on a hosted runner and
/// does not reproduce on a developer machine — 64 000 file operations produced none, with the
/// buffer at 8 KiB or at 64 KiB. So the decision it turns on is measured here, where a storm is not
/// needed to ask the question.
/// </summary>
public sealed class WatchErrorPolicyTests
{
    [Fact]
    public void An_overflowed_buffer_means_events_were_lost()
    {
        Assert.True(WatchErrorPolicy.MeansEventsWereLost(
            new InternalBufferOverflowException("Too many changes at once in directory.")));
    }

    [Theory]
    [MemberData(nameof(ErrorsThatEndTheWatching))]
    public void Every_other_failure_ends_the_watching(Exception error) =>
        Assert.False(WatchErrorPolicy.MeansEventsWereLost(error));

    [Fact]
    public void The_policy_refuses_to_judge_nothing() =>
        Assert.Throws<ArgumentNullException>(() => WatchErrorPolicy.MeansEventsWereLost(null!));

    public static TheoryData<Exception> ErrorsThatEndTheWatching() =>
    [
        // The root stopped answering, which is what an unplugged USB disk and a dropped network
        // share both look like from here.
        new IOException("The specified network name is no longer available."),
        new UnauthorizedAccessException("Access to the path is denied."),
        new NotSupportedException("The path is not a supported watch target."),
        new ObjectDisposedException(nameof(FileSystemWatcher)),
    ];
}
