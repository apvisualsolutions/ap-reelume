// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Playback;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation;
using ApSolutions.LocalMedia.Presentation.Player;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// Subtitle customisation and track selection must stay operable and legible: every control carries
/// an accessible name, the values stay inside the domain range, and the preview never hides the text
/// at 100%, 150%, or 200% scaling.
/// </summary>
public sealed class SubtitleStyleTests
{
    [AvaloniaFact]
    public async Task The_style_persists_by_scope_and_restores_into_a_new_view_model()
    {
        var repository = new InMemoryPreferenceRepository();
        var first = new SubtitleStyleViewModel(repository)
        {
            FontSizePercent = 175,
            FontFamily = "Verdana",
            ForegroundHex = "#FFFFFF00",
            BackgroundHex = "#80000000",
            BackgroundOpacity = 0.4,
            OutlineThickness = 3,
        };

        await first.SaveAsync(TestContext.Current.CancellationToken);
        var restored = new SubtitleStyleViewModel(repository);
        await restored.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(175, restored.FontSizePercent);
        Assert.Equal("Verdana", restored.FontFamily);
        Assert.Equal("#FFFFFF00", restored.ForegroundHex);
        Assert.Equal(0.4, restored.BackgroundOpacity);
        Assert.Equal(3, restored.OutlineThickness);

        await restored.ResetAsync(TestContext.Current.CancellationToken);
        var afterReset = new SubtitleStyleViewModel(repository);
        await afterReset.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(SubtitleStyle.EngineDefault, afterReset.Style);
    }

    [AvaloniaFact]
    public async Task Out_of_range_values_are_clamped_before_they_can_hide_the_text()
    {
        var repository = new InMemoryPreferenceRepository();
        var viewModel = new SubtitleStyleViewModel(repository)
        {
            FontSizePercent = 5000,
            BackgroundOpacity = -2,
            OutlineThickness = 99,
        };

        Assert.Equal(SubtitleStyle.MaximumFontSizePercent, viewModel.FontSizePercent);
        Assert.Equal(0, viewModel.BackgroundOpacity);
        Assert.Equal(SubtitleStyle.MaximumOutlineThickness, viewModel.OutlineThickness);

        viewModel.FontSizePercent = 1;
        Assert.Equal(SubtitleStyle.MinimumFontSizePercent, viewModel.FontSizePercent);
        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            TestContext.Current.CancellationToken);
        Assert.Equal(SubtitleStyle.MinimumFontSizePercent, stored!.SubtitleStyle!.FontSizePercent);
    }

    [AvaloniaFact]
    public void Every_style_control_is_named_focusable_and_rendered_at_each_supported_scaling()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var captures = Path.Combine(RepositoryLayout.Root, "artifacts", "ui-captures", "T20");
        Directory.CreateDirectory(captures);

        foreach (var cultureName in new[] { "es-ES", "en-US" })
        {
            App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo(cultureName));
            foreach (var scaling in new[] { 1.0, 1.5, 2.0 })
            {
                var view = new SubtitleStyleView
                {
                    DataContext = new SubtitleStyleViewModel(new InMemoryPreferenceRepository()),
                };
                var window = new Window { Width = 520, Height = 640, Content = view };
                window.SetRenderScaling(scaling);
                window.Show();
                Dispatcher.UIThread.RunJobs();

                // Only the controls this view declares are asserted; template parts of a ComboBox
                // are named by the theme, not by the application.
                var declared = new[]
                {
                    "SubtitleSizeSlider",
                    "SubtitleFamilySelector",
                    "SubtitleForegroundFirst",
                    "SubtitleBackgroundFirst",
                    "SubtitleBackgroundOpacitySlider",
                    "SubtitleOutlineSlider",
                };
                var controls = view.GetVisualDescendants()
                    .OfType<Control>()
                    .Where(control => declared.Contains(control.Name, StringComparer.Ordinal))
                    .ToArray();
                Assert.Equal(declared.Length, controls.Length);
                Assert.All(controls, control => Assert.False(
                    string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)),
                    $"{control.GetType().Name} has no accessible name."));
                Assert.All(controls, control => Assert.True(control.Focusable));

                var preview = view.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(block => block.Name == "SubtitlePreviewText");
                Assert.False(string.IsNullOrWhiteSpace(preview.Text));
                Assert.True(preview.Bounds.Height > 0, $"The preview collapsed at {scaling:P0}.");

                if (cultureName == "es-ES")
                {
                    var frame = window.CaptureRenderedFrame();
                    Assert.NotNull(frame);
                    frame.Save(
                        Path.Combine(captures, $"subtitle-style-scale-{(int)(scaling * 100)}.png"),
                        PngBitmapEncoderOptions.Default);
                }

                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void The_preview_stays_legible_under_high_contrast()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var previous = Avalonia.Application.Current.RequestedThemeVariant;
        Avalonia.Application.Current.RequestedThemeVariant =
            new Avalonia.Styling.ThemeVariant("HighContrast", Avalonia.Styling.ThemeVariant.Light);
        try
        {
            var view = new SubtitleStyleView
            {
                DataContext = new SubtitleStyleViewModel(new InMemoryPreferenceRepository()),
            };
            var window = new Window { Width = 520, Height = 640, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var preview = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(block => block.Name == "SubtitlePreviewText");
            Assert.True(preview.Bounds.Height > 0);
            Assert.False(string.IsNullOrWhiteSpace(preview.Text));

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var captures = Path.Combine(RepositoryLayout.Root, "artifacts", "ui-captures", "T20");
            Directory.CreateDirectory(captures);
            frame.Save(
                Path.Combine(captures, "subtitle-style-high-contrast.png"),
                PngBitmapEncoderOptions.Default);
            window.Close();
        }
        finally
        {
            Avalonia.Application.Current.RequestedThemeVariant = previous;
        }
    }

    [AvaloniaFact]
    public async Task The_track_selector_lists_tracks_by_attribute_and_stores_the_chosen_scope()
    {
        Assert.NotNull(Avalonia.Application.Current);
        App.ApplyLanguage(Avalonia.Application.Current!, CultureInfo.GetCultureInfo("es-ES"));
        var repository = new InMemoryPreferenceRepository();
        var engine = new RecordingEngine();
        var viewModel = new TrackSelectorViewModel(
            new SelectTrack(engine, repository),
            PlaybackPreference.FileKey(Guid.Empty),
            PlaybackPreference.SeriesKey(Guid.Empty));
        var spanish = new MediaTrack("2", MediaTrackKind.Audio, "spa", "Español 5.1", 6, "eac3");
        var english = new MediaTrack("1", MediaTrackKind.Audio, "eng", "English", 2, "aac");
        var subtitle = new MediaTrack("5", MediaTrackKind.Subtitle, "spa", "Español", null, "subrip");

        viewModel.Load([english, spanish, subtitle], spanish, activeSubtitle: null);

        Assert.Equal(2, viewModel.AudioTracks.Count);
        Assert.Equal(2, viewModel.SubtitleTracks.Count);
        Assert.True(viewModel.SubtitleTracks[0].IsDisabled);
        Assert.Equal(spanish, viewModel.SelectedAudio!.Track);
        Assert.Contains("5.1", viewModel.AudioTracks.Single(o => o.Track == spanish).Display, StringComparison.Ordinal);

        viewModel.RememberForSeries = true;
        await viewModel.ApplyAsync(MediaTrackKind.Audio, TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(
            PreferenceScope.Series,
            PlaybackPreference.SeriesKey(Guid.Empty),
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("spa", stored!.Audio!.Language);
        Assert.Equal(6, stored.Audio.Channels);
        Assert.Equal("2", engine.LastAudioTrackId);

        var view = new TrackSelectorView { DataContext = viewModel };
        var window = new Window { Width = 420, Height = 300, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var boxes = view.GetVisualDescendants().OfType<ComboBox>().ToArray();
        Assert.Equal(2, boxes.Length);
        Assert.All(boxes, box => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(box))));
        window.Close();
    }

    /// <summary>
    /// Choosing a track applies it. A list that changes its label and leaves the media as it was is a
    /// control that does nothing, and walking the real application is what showed it doing nothing.
    /// </summary>
    [AvaloniaFact]
    public async Task Choosing_a_track_applies_it_to_the_session_and_stores_the_choice()
    {
        var repository = new InMemoryPreferenceRepository();
        var engine = new RecordingEngine();
        var viewModel = new TrackSelectorViewModel(
            new SelectTrack(engine, repository),
            PlaybackPreference.FileKey(Guid.Empty));
        var spanish = new MediaTrack("2", MediaTrackKind.Audio, "spa", "Español 5.1", 6, "eac3");
        var english = new MediaTrack("1", MediaTrackKind.Audio, "eng", "English", 2, "aac");
        viewModel.Load([english, spanish], english, activeSubtitle: null);
        Assert.Null(engine.LastAudioTrackId);

        viewModel.SelectedAudio = viewModel.AudioTracks.Single(option => option.Track == spanish);
        for (var attempt = 0; attempt < 100 && engine.LastAudioTrackId is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Equal("2", engine.LastAudioTrackId);
        var stored = await repository.GetAsync(
            PreferenceScope.File,
            PlaybackPreference.FileKey(Guid.Empty),
            TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal("spa", stored!.Audio!.Language);
    }

    [AvaloniaFact]
    public async Task Turning_subtitles_off_is_a_choice_and_is_stored_like_any_other()
    {
        var repository = new InMemoryPreferenceRepository();
        var engine = new RecordingEngine();
        var viewModel = new TrackSelectorViewModel(
            new SelectTrack(engine, repository),
            PlaybackPreference.FileKey(Guid.Empty),
            PlaybackPreference.SeriesKey(Guid.Empty));
        var subtitle = new MediaTrack("5", MediaTrackKind.Subtitle, "spa", "Español", null, "subrip");
        viewModel.Load([subtitle], activeAudio: null, subtitle);
        viewModel.RememberForSeries = true;

        viewModel.SelectedSubtitle = viewModel.SubtitleTracks.Single(option => option.IsDisabled);
        PlaybackPreference? stored = null;
        for (var attempt = 0; attempt < 100 && stored is null; attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
            stored = await repository.GetAsync(
                PreferenceScope.Series,
                PlaybackPreference.SeriesKey(Guid.Empty),
                TestContext.Current.CancellationToken);
        }

        Assert.NotNull(stored);
        Assert.Null(stored!.Subtitle);
    }

    /// <summary>Showing what is already playing is not a choice, so loading stores nothing.</summary>
    [AvaloniaFact]
    public void Loading_the_tracks_of_a_new_file_stores_nothing()
    {
        var repository = new InMemoryPreferenceRepository();
        var engine = new RecordingEngine();
        var viewModel = new TrackSelectorViewModel(
            new SelectTrack(engine, repository),
            PlaybackPreference.FileKey(Guid.Empty));

        viewModel.Load(
            [new MediaTrack("1", MediaTrackKind.Audio, "eng", "English", 2, "aac")],
            activeAudio: null,
            activeSubtitle: null);

        Assert.Null(engine.LastAudioTrackId);
    }

    private sealed class RecordingEngine : IMediaPlayerEngine
    {
#pragma warning disable CS0067 // The contract declares these events; this double never raises them.
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;

        public event EventHandler<PlaybackPositionChangedEventArgs>? PositionChanged;

        public event EventHandler<PlaybackFailureEventArgs>? Failure;
#pragma warning restore CS0067

        public PlaybackState State => PlaybackState.Playing;

        public string? LastAudioTrackId { get; private set; }

        public string? LastSubtitleTrackId { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task OpenAsync(PlaybackRequest request, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<PlaybackSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(PlaybackSnapshot.Create(State, TimeSpan.Zero, TimeSpan.FromMinutes(1), []));

        public Task SelectTrackAsync(
            MediaTrackKind kind,
            string? trackId,
            CancellationToken cancellationToken = default)
        {
            if (kind == MediaTrackKind.Audio)
            {
                LastAudioTrackId = trackId;
            }
            else
            {
                LastSubtitleTrackId = trackId;
            }

            return Task.CompletedTask;
        }

        public Task<MediaTrack> AddExternalSubtitleAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MediaTrack(path, MediaTrackKind.Subtitle, IsExternal: true));

        public Task SetSpeedAsync(double multiplier, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task SetAudioOutputDeviceAsync(string deviceId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ApplyVolumeAsync(VolumeDecision decision, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InMemoryPreferenceRepository : IPlaybackPreferenceRepository
    {
        private readonly Dictionary<(PreferenceScope, string), PlaybackPreference> _stored = [];

        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue((scope, scopeKey), out var value) ? value : null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preference);
            _stored[(preference.Scope, preference.ScopeKey)] = preference;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default)
        {
            _ = _stored.Remove((scope, scopeKey));
            return Task.CompletedTask;
        }
    }
}
