// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Shortcuts and media keys existed as parts and never as a chain (PLY-014 / ARQ-002): the media
/// key source was registered and its <c>StartAsync</c> never called, the router that stops a key
/// from acting twice was never instantiated, the player had no key handling at all, and the
/// settings editor could edit a map of its own instead of the one the application registered.
/// </summary>
public sealed class ShortcutWiringTests
{
    [Fact]
    public void Opening_a_session_starts_the_media_key_source()
    {
        var composition = CompositionSource();

        Assert.Contains("GetRequiredService<IMediaKeySource>", composition, StringComparison.Ordinal);
        Assert.Contains("mediaKeys.StartAsync", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_session_input_runs_through_one_router()
    {
        var composition = CompositionSource();

        Assert.Contains("new InputCommandRouter(", composition, StringComparison.Ordinal);
        Assert.Contains("InputOrigin.MediaKey", composition, StringComparison.Ordinal);
        Assert.Contains("InputOrigin.Keyboard", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void Media_keys_arrive_on_the_interface_thread_not_on_the_pump()
    {
        var composition = CompositionSource();

        // CommandReceived is raised from the STA pump thread; dispatching without marshalling
        // would touch view models on the wrong thread.
        Assert.Contains("CommandReceived +=", composition, StringComparison.Ordinal);
        var handler = composition.IndexOf("void OnMediaKey", StringComparison.Ordinal);
        Assert.True(handler >= 0, "The media key handler has no name to inspect.");
        Assert.Contains(
            "Dispatcher.UIThread.Post",
            composition[handler..Math.Min(composition.Length, handler + 400)],
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_player_resolves_key_gestures_through_the_shortcut_map()
    {
        var composition = CompositionSource();
        var playerView = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Player",
            "PlayerView.axaml.cs"));

        Assert.Contains("GetRequiredService<ShortcutMap>", composition, StringComparison.Ordinal);
        Assert.Contains("GestureHandler", composition, StringComparison.Ordinal);
        Assert.Contains("OnKeyDown", playerView, StringComparison.Ordinal);
        Assert.Contains("GestureHandler", playerView, StringComparison.Ordinal);
    }

    [Fact]
    public void Closing_the_session_stops_listening_for_media_keys()
    {
        var composition = CompositionSource();

        Assert.Contains("CommandReceived -=", composition, StringComparison.Ordinal);
        Assert.Contains("StopMediaKeysQuietlyAsync", composition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_settings_surface_edits_the_map_the_player_reads()
    {
        var settings = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Presentation",
            "Player",
            "ShortcutSettingsViewModel.cs"));

        // An optional map with a "?? new" fallback can silently edit a second map that no key
        // press ever reads; the editor must demand the one the application registered.
        Assert.DoesNotContain("?? new ShortcutMap()", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ShortcutMap? map = null", settings, StringComparison.Ordinal);
    }

    private static string CompositionSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "src",
            "ApSolutions.LocalMedia.Windows",
            "CompositionRoot.cs");
        Assert.True(File.Exists(path), "CompositionRoot.cs was not found where the host keeps it.");
        return File.ReadAllText(path);
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
