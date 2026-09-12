// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ApSolutions.LocalMedia.Domain.Continuity;
using ApSolutions.LocalMedia.Domain.Playback;
using ApSolutions.LocalMedia.Presentation.Commands;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Brightness, contrast and gamma while the film is on screen, stored for the scope the person is
/// in and pushed at the engine the instant a control moves (PLY-018, ADR-0012).
/// </summary>
/// <remarks>
/// <para>
/// It writes to the engine on every change rather than on the way out, and that is the reason this
/// panel lives in the player at all: the value a person wants is the one they can see, so a control
/// whose effect arrived later would be a control nobody could judge.
/// </para>
/// <para>
/// It clamps where <see cref="PictureAdjustment"/> refuses, and the two are not in disagreement. The
/// domain refuses because a value out of range reaches it from a file somebody edited and watching a
/// different picture than the one asked for is how a defect survives being looked at. Here the
/// caller is a slider already bound to the same range, and the only way past its end is arithmetic —
/// where throwing would take the window down inside a binding.
/// </para>
/// </remarks>
public sealed class PictureAdjustmentViewModel : INotifyPropertyChanged
{
    private readonly IPlaybackPreferenceRepository _repository;
    private readonly IPictureAdjustable _target;
    private readonly PreferenceScope _scope;
    private readonly string _scopeKey;
    private PictureAdjustment _adjustment = PictureAdjustment.Neutral;
    private bool _suppressAutoSave;

    public PictureAdjustmentViewModel(
        IPlaybackPreferenceRepository repository,
        IPictureAdjustable target,
        PreferenceScope scope = PreferenceScope.Global,
        string? scopeKey = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _scope = scope;
        _scopeKey = scopeKey ?? PlaybackPreference.GlobalKey;
        ResetCommand = new AsyncRelayCommand(() => ResetAsync(CancellationToken.None));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The «Restaurar valores por defecto» of this group (UX-010).</summary>
    public ICommand ResetCommand { get; }

    public static double MinimumBrightness => PictureAdjustment.MinimumBrightness;

    public static double MaximumBrightness => PictureAdjustment.MaximumBrightness;

    public static double MinimumContrast => PictureAdjustment.MinimumContrast;

    public static double MaximumContrast => PictureAdjustment.MaximumContrast;

    public static double MinimumGamma => PictureAdjustment.MinimumGamma;

    public static double MaximumGamma => PictureAdjustment.MaximumGamma;

    public PictureAdjustment Adjustment => _adjustment;

    /// <summary>Whether the picture is being left exactly as it arrived.</summary>
    public bool IsNeutral => _adjustment.IsNeutral;

    // Each setter rebuilds the record rather than using «with», and that is not style: the three
    // properties are declared get-only on top of the positional ones so the validation runs, which
    // leaves «with» unable to assign any of them.
    public double Brightness
    {
        get => _adjustment.Brightness;
        set => Update(new PictureAdjustment(
            Clamp(value, MinimumBrightness, MaximumBrightness),
            _adjustment.Contrast,
            _adjustment.Gamma));
    }

    public double Contrast
    {
        get => _adjustment.Contrast;
        set => Update(new PictureAdjustment(
            _adjustment.Brightness,
            Clamp(value, MinimumContrast, MaximumContrast),
            _adjustment.Gamma));
    }

    public double Gamma
    {
        get => _adjustment.Gamma;
        set => Update(new PictureAdjustment(
            _adjustment.Brightness,
            _adjustment.Contrast,
            Clamp(value, MinimumGamma, MaximumGamma)));
    }

    /// <summary>Reads the scope's stored adjustment, or the neutral when it stored none.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _suppressAutoSave = true;
        try
        {
            var stored = await _repository.GetAsync(_scope, _scopeKey, cancellationToken).ConfigureAwait(true);
            Update(stored?.Picture ?? PictureAdjustment.Neutral);
        }
        finally
        {
            _suppressAutoSave = false;
        }
    }

    /// <summary>Persists the adjustment without disturbing the other fields of the scope.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAsync(_scope, _scopeKey, cancellationToken).ConfigureAwait(true)
            ?? new PlaybackPreference { Scope = _scope, ScopeKey = _scopeKey };
        await _repository
            .SaveAsync(stored with { Picture = _adjustment }, cancellationToken)
            .ConfigureAwait(true);
    }

    /// <summary>
    /// Puts the three back where they came from and stores that, rather than clearing the field:
    /// a neutral somebody chose has to beat whatever a wider scope says, or undoing an adjustment
    /// for one film would hand it the global one instead.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        // The automatic save is held off and the store is awaited instead, so a caller that waits
        // for this knows the neutral is on disk. Letting both run would write the same row twice and
        // would leave the awaited one indistinguishable from its own absence — measured by taking it
        // out and watching the test stay green on the fire-and-forget behind it.
        _suppressAutoSave = true;
        try
        {
            Update(PictureAdjustment.Neutral);
        }
        finally
        {
            _suppressAutoSave = false;
        }

        await SaveAsync(cancellationToken).ConfigureAwait(true);
    }

    private static double Clamp(double value, double minimum, double maximum) =>
        double.IsNaN(value) ? minimum : Math.Clamp(value, minimum, maximum);

    private void Update(PictureAdjustment adjustment)
    {
        if (_adjustment == adjustment)
        {
            return;
        }

        _adjustment = adjustment;
        _target.PictureAdjustment = adjustment;

        // Every setter here goes through this, so this is the one place a change can be stored from,
        // and it stores rather than waiting to be asked — the same reason the subtitle style does.
        // Loading is excluded because showing what is already stored is not a change worth storing
        // back, and so is resetting, which awaits its own store.
        if (!_suppressAutoSave)
        {
            _ = SaveAsync();
        }

        foreach (var name in new[]
        {
            nameof(Adjustment),
            nameof(Brightness),
            nameof(Contrast),
            nameof(Gamma),
            nameof(IsNeutral),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
