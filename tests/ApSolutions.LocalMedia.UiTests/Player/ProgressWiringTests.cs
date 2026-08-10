// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The five-second promise is only real when the assembly runs the loop that keeps it: the tracker's
/// periodic write has to be started, the pause and seek moments have to flush, and the position
/// handler has to be detached when its session ends. The deep audit found the loop invoked from
/// tests alone (BUG-003) and the handler accumulating once per session (BUG-007).
/// </summary>
public sealed class ProgressWiringTests
{
    [Fact]
    public void The_periodic_save_loop_is_started_by_the_assembly()
    {
        // ProgressPolicy.SaveInterval exists so a crash costs five seconds, not a session. The loop
        // that honours it is RunAsync, and only tests were calling it.
        Assert.Contains(".RunAsync(", CompositionRootSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Pausing_flushes_the_position()
    {
        Assert.Contains("PersistenceTrigger.Pause", CompositionRootSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void Seeking_flushes_the_position()
    {
        Assert.Contains("PersistenceTrigger.Seek", CompositionRootSource(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_position_handler_is_detached_when_its_session_ends()
    {
        // The engine is a singleton and the handler captures per-session state: subscribing on every
        // open without the matching unsubscribe stacks one dead session's work on the next.
        Assert.Contains("PositionChanged -=", CompositionRootSource(), StringComparison.Ordinal);
    }

    private static string CompositionRootSource()
    {
        return CompositionSourceText.Read();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ApSolutions.LocalMedia.sln")))
        {
            directory = directory.Parent!;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
