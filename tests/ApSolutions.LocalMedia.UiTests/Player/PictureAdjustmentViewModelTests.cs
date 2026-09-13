// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Player;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Player;

/// <summary>
/// The three picture controls: what they store, what reaches the engine, and what a value outside
/// the domain's range does to a slider that is already bound to that range.
/// </summary>
public sealed class PictureAdjustmentViewModelTests
{
    [Fact]
    public async Task The_adjustment_persists_by_scope_and_restores_into_a_new_view_model()
    {
        var repository = new InMemoryPreferences();
        var engine = new RecordingTarget();
        var first = new PictureAdjustmentViewModel(repository, engine)
        {
            Brightness = 0.25,
            Contrast = 1.4,
            Gamma = 1.8,
        };

        await first.SaveAsync(TestContext.Current.CancellationToken);
        var restored = new PictureAdjustmentViewModel(repository, engine);
        await restored.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0.25, restored.Brightness);
        Assert.Equal(1.4, restored.Contrast);
        Assert.Equal(1.8, restored.Gamma);
        Assert.False(restored.IsNeutral);
    }

    /// <summary>
    /// The scope is what the constructor was given, and it reaches the row that was written. Every
    /// other test here builds the model with no scope at all, so all of them would pass with the two
    /// parameters thrown away and everything stored globally — which is the whole of «remembered for
    /// this show» gone, measured by doing exactly that and watching 1354 tests stay green.
    /// </summary>
    [Theory]
    [InlineData(PreferenceScope.Series, "series:one")]
    [InlineData(PreferenceScope.File, "file:one")]
    public async Task The_adjustment_is_stored_in_the_scope_it_was_given(PreferenceScope scope, string key)
    {
        var repository = new InMemoryPreferences();
        var model = new PictureAdjustmentViewModel(repository, new RecordingTarget(), scope, key)
        {
            Gamma = 1.6,
        };

        await model.SaveAsync(TestContext.Current.CancellationToken);

        var inScope = await repository.GetAsync(scope, key, TestContext.Current.CancellationToken);
        Assert.Equal(new PictureAdjustment(0d, 1d, 1.6), inScope!.Picture);

        var global = await repository.GetAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            TestContext.Current.CancellationToken);
        Assert.Null(global);
    }

    /// <summary>
    /// Storing the picture leaves the rest of the scope alone. Nothing else here puts a neighbouring
    /// field in the row, so a save that built a fresh preference instead of reading the stored one
    /// would pass every test in this file while wiping the speed, the volume, the chosen tracks and
    /// the output device of that show every time somebody moved a slider.
    /// </summary>
    [Fact]
    public async Task Storing_the_picture_leaves_the_rest_of_the_scope_alone()
    {
        var repository = new InMemoryPreferences();
        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.Series,
                ScopeKey = "series:one",
                VolumePercent = 80,
                SpeedMultiplier = 1.25,
            },
            TestContext.Current.CancellationToken);

        var model = new PictureAdjustmentViewModel(
            repository,
            new RecordingTarget(),
            PreferenceScope.Series,
            "series:one")
        {
            Gamma = 1.6,
        };
        await model.SaveAsync(TestContext.Current.CancellationToken);

        var stored = await repository.GetAsync(
            PreferenceScope.Series,
            "series:one",
            TestContext.Current.CancellationToken);
        Assert.Equal(80, stored!.VolumePercent);
        Assert.Equal(1.25, stored.SpeedMultiplier);
        Assert.Equal(new PictureAdjustment(0d, 1d, 1.6), stored.Picture);
    }

    /// <summary>
    /// The picture has to change while the control moves, which is the whole point of deciding this
    /// one while watching: a value that only reached the engine on the next file would be a control
    /// nobody could judge.
    /// </summary>
    [Fact]
    public void Moving_a_control_reaches_the_engine_at_once()
    {
        var engine = new RecordingTarget();
        var model = new PictureAdjustmentViewModel(new InMemoryPreferences(), engine);

        Assert.Equal(PictureAdjustment.Neutral, engine.PictureAdjustment);

        model.Gamma = 1.6;

        Assert.Equal(new PictureAdjustment(0d, 1d, 1.6), engine.PictureAdjustment);
    }

    /// <summary>
    /// Reset puts the three back and stores that, which is <c>UX-010</c>'s half of this panel. It
    /// stores rather than clearing, because a neutral somebody chose has to beat a wider scope.
    /// </summary>
    [Fact]
    public async Task Restoring_the_defaults_stores_the_neutral_rather_than_forgetting_it()
    {
        var repository = new InMemoryPreferences();
        var engine = new RecordingTarget();
        var model = new PictureAdjustmentViewModel(repository, engine) { Gamma = 2.2 };
        await model.SaveAsync(TestContext.Current.CancellationToken);

        // Named before the reset, because «the engine is neutral afterwards» is also what an engine
        // nobody ever wrote to answers — measured by taking the write out and watching this pass.
        Assert.Equal(new PictureAdjustment(0d, 1d, 2.2), engine.PictureAdjustment);

        await model.ResetAsync(TestContext.Current.CancellationToken);

        Assert.True(model.IsNeutral);
        Assert.Equal(PictureAdjustment.Neutral, engine.PictureAdjustment);
        var stored = await repository.GetAsync(
            PreferenceScope.Global,
            PlaybackPreference.GlobalKey,
            TestContext.Current.CancellationToken);
        Assert.Equal(PictureAdjustment.Neutral, stored!.Picture);
    }

    /// <summary>
    /// A slider bound to the domain's own range can still hand over a value a hair outside it, and
    /// the domain refuses rather than clamping. Refusing here would throw inside a binding, which
    /// takes the window down; the panel clamps and the domain keeps its refusal for callers that
    /// are not a slider.
    /// </summary>
    [Theory]
    [InlineData(-9d, PictureAdjustment.MinimumBrightness)]
    [InlineData(9d, PictureAdjustment.MaximumBrightness)]
    // The row that justifies the whole guard, and it was missing: NaN fails every comparison, so
    // Math.Clamp hands it straight back and the domain refuses it — inside a binding, which is the
    // window going down. The two rows above are caught by Math.Clamp alone.
    [InlineData(double.NaN, PictureAdjustment.MinimumBrightness)]
    public void A_value_past_the_end_of_its_slider_is_clamped_instead_of_throwing(
        double asked,
        double expected)
    {
        var model = new PictureAdjustmentViewModel(new InMemoryPreferences(), new RecordingTarget())
        {
            Brightness = asked,
        };

        Assert.Equal(expected, model.Brightness);
    }

    /// <summary>
    /// Asking for the value a control already has stores nothing. Every setter goes through one
    /// place and that place stores, so without the equality guard a panel that reloads its own
    /// values would write them straight back — and moving a slider one way and back would leave a
    /// stored decision where there had been a silence.
    /// </summary>
    [Fact]
    public void Setting_a_control_to_the_value_it_already_carries_stores_nothing()
    {
        var repository = new InMemoryPreferences();
        var model = new PictureAdjustmentViewModel(repository, new RecordingTarget());

        model.Brightness = 0d;
        model.Contrast = 1d;
        model.Gamma = 1d;

        Assert.Equal(0, repository.Saves);
    }

    /// <summary>
    /// Moving one control announces all five, which is what keeps the number beside each slider and
    /// the reset button itself in step with the value. Nothing here subscribed until this test, so
    /// the announcement was reaching nobody and the branch that makes it was never taken.
    /// </summary>
    [Fact]
    public void Moving_a_control_announces_the_value_and_the_neutral_it_is_no_longer_at()
    {
        var model = new PictureAdjustmentViewModel(new InMemoryPreferences(), new RecordingTarget());
        var announced = new List<string?>();
        model.PropertyChanged += (_, args) => announced.Add(args.PropertyName);

        model.Gamma = 1.6;

        Assert.Contains(nameof(PictureAdjustmentViewModel.Gamma), announced);
        Assert.Contains(nameof(PictureAdjustmentViewModel.IsNeutral), announced);
        Assert.Contains(nameof(PictureAdjustmentViewModel.Adjustment), announced);
    }

    /// <summary>
    /// A scope with a row in it but no picture in that row opens neutral, which is not the same
    /// path as a scope with no row at all: the first is somebody who set a volume here and never
    /// touched the picture, and it is the ordinary case for a library already in use.
    /// </summary>
    [Fact]
    public async Task A_scope_that_stored_something_else_opens_at_neutral()
    {
        var repository = new InMemoryPreferences();
        await repository.SaveAsync(
            new PlaybackPreference
            {
                Scope = PreferenceScope.Global,
                ScopeKey = PlaybackPreference.GlobalKey,
                VolumePercent = 80,
            },
            TestContext.Current.CancellationToken);
        var model = new PictureAdjustmentViewModel(repository, new RecordingTarget());

        await model.LoadAsync(TestContext.Current.CancellationToken);

        Assert.True(model.IsNeutral);
    }

    [Fact]
    public void A_panel_with_nothing_to_store_in_or_nothing_to_adjust_is_refused()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PictureAdjustmentViewModel(null!, new RecordingTarget()));
        Assert.Throws<ArgumentNullException>(
            () => new PictureAdjustmentViewModel(new InMemoryPreferences(), null!));
    }

    /// <summary>What the engine was last told, which is the only thing a person can see.</summary>
    private sealed class RecordingTarget : IPictureAdjustable
    {
        public PictureAdjustment PictureAdjustment { get; set; } = PictureAdjustment.Neutral;
    }

    private sealed class InMemoryPreferences : IPlaybackPreferenceRepository
    {
        private readonly Dictionary<(PreferenceScope, string), PlaybackPreference> _stored = [];

        /// <summary>How many times anything asked for a row to be written.</summary>
        public int Saves { get; private set; }

        public Task<PlaybackPreference?> GetAsync(
            PreferenceScope scope,
            string scopeKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_stored.TryGetValue((scope, scopeKey), out var value) ? value : null);

        public Task SaveAsync(PlaybackPreference preference, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(preference);
            Saves++;
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
