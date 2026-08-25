// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ApSolutions.LocalMedia.Domain.Appearance;
using ApSolutions.LocalMedia.Domain.Continuity;

namespace ApSolutions.LocalMedia.Presentation.Player;

/// <summary>
/// Subtitle presentation the person controls: size, safe family, colours, background opacity, and
/// outline. Every value is clamped by the domain, so no stored preference can hide the text.
/// </summary>
public sealed class SubtitleStyleViewModel : INotifyPropertyChanged
{
    private readonly IPlaybackPreferenceRepository _repository;
    private readonly PreferenceScope _scope;
    private readonly string _scopeKey;
    private SubtitleStyle _style = SubtitleStyle.EngineDefault;
    private bool _isLoading;
    private readonly SwatchCommand _applyForeground;
    private readonly SwatchCommand _applyBackground;

    public SubtitleStyleViewModel(
        IPlaybackPreferenceRepository repository,
        PreferenceScope scope = PreferenceScope.Global,
        string? scopeKey = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _scope = scope;
        _scopeKey = scopeKey ?? PlaybackPreference.GlobalKey;
        _applyForeground = new SwatchCommand(value => ForegroundHex = WithRgb(ForegroundHex, value));
        _applyBackground = new SwatchCommand(value => BackgroundHex = WithRgb(BackgroundHex, value));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Families that ship with Windows 11 and stay legible at every scaling.</summary>
    public static IReadOnlyList<string> SafeFontFamilies { get; } =
        ["Segoe UI", "Segoe UI Variable", "Arial", "Verdana", "Tahoma", "Consolas"];

    /// <summary>
    /// Six inks and six grounds, which is the shape the prototype gives a colour: swatches and the
    /// value beside them, never a field to type six hexadecimal digits into.
    /// </summary>
    /// <remarks>
    /// The six are chosen for what subtitles are drawn over rather than for variety. White is the
    /// default every player ships; ivory is white with the glare taken off it; the yellow is the one
    /// optical discs standardised on because it survives a bright scene; the cyan is what a second
    /// speaker is traditionally given; the pale grey is for a picture that is mostly white; and the
    /// near-black is the one ink that works on the white ground below.
    /// <para>
    /// Nothing here decides whether an ink and a ground read against each other, and that is
    /// deliberate: this is the person's own picture and their own eyes, and the domain already
    /// clamps what would hide the text altogether.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> ForegroundSwatches { get; } =
        ["#FFFFFF", "#F5F0E1", "#F2E205", "#7CE0E8", "#C3CEDB", "#0A0C10"];

    /// <summary>
    /// Six grounds, from the black a letterbox already is to the near-white a dark ink needs.
    /// </summary>
    /// <remarks>
    /// The pale one is <c>#E9EEF4</c> rather than white, and that is not a taste: a swatch is
    /// announced by the colour it carries, and a white ground beside the white ink would be two
    /// controls answering to one name — which a screen reader cannot tell apart and the walk refuses
    /// outright.
    /// </remarks>
    /// <summary>The grid both pickers open with, which is the domain's and not this page's.</summary>
    public static IReadOnlyList<string> ColourGrid => AccentPalette.Grid;

    public static IReadOnlyList<string> BackgroundSwatches { get; } =
        ["#000000", "#0B0D10", "#1F2937", "#0B1E2F", "#2A1D12", "#E9EEF4"];

    /// <summary>
    /// Which swatch is the one in force, as a flag the style reads rather than a glyph on top of it.
    /// </summary>
    /// <remarks>
    /// A ring around the chosen circle, which is what the prototype draws and what the owner asked
    /// for: a ● inside a circle of colour reads as a radio button somebody dropped on a swatch. It
    /// is still geometry, so both high contrast dictionaries keep the cue the glyph was there for.
    /// </remarks>
    public bool IsFirstForeground => Chosen(ForegroundSwatches[0], ForegroundHex);

    public bool IsSecondForeground => Chosen(ForegroundSwatches[1], ForegroundHex);

    public bool IsThirdForeground => Chosen(ForegroundSwatches[2], ForegroundHex);

    public bool IsFourthForeground => Chosen(ForegroundSwatches[3], ForegroundHex);

    public bool IsFifthForeground => Chosen(ForegroundSwatches[4], ForegroundHex);

    public bool IsSixthForeground => Chosen(ForegroundSwatches[5], ForegroundHex);

    public bool IsCustomForeground => !ForegroundSwatches.Any(swatch => Chosen(swatch, ForegroundHex));

    public bool IsFirstBackground => Chosen(BackgroundSwatches[0], BackgroundHex);

    public bool IsSecondBackground => Chosen(BackgroundSwatches[1], BackgroundHex);

    public bool IsThirdBackground => Chosen(BackgroundSwatches[2], BackgroundHex);

    public bool IsFourthBackground => Chosen(BackgroundSwatches[3], BackgroundHex);

    public bool IsFifthBackground => Chosen(BackgroundSwatches[4], BackgroundHex);

    public bool IsSixthBackground => Chosen(BackgroundSwatches[5], BackgroundHex);

    public bool IsCustomBackground => !BackgroundSwatches.Any(swatch => Chosen(swatch, BackgroundHex));

    /// <summary>The three the ink's picker moves, and the three the ground's does.</summary>
    public double ForegroundHue
    {
        get => AccentPalette.Split(Rgb(ForegroundHex)).Hue;
        set => ForegroundHex = WithRgb(ForegroundHex, AccentPalette.Join(value, ForegroundSaturation, ForegroundLightness));
    }

    public double ForegroundSaturation
    {
        get => AccentPalette.Split(Rgb(ForegroundHex)).Saturation;
        set => ForegroundHex = WithRgb(ForegroundHex, AccentPalette.Join(ForegroundHue, value, ForegroundLightness));
    }

    public double ForegroundLightness
    {
        get => AccentPalette.Split(Rgb(ForegroundHex)).Lightness;
        set => ForegroundHex = WithRgb(ForegroundHex, AccentPalette.Join(ForegroundHue, ForegroundSaturation, value));
    }

    public double BackgroundHue
    {
        get => AccentPalette.Split(Rgb(BackgroundHex)).Hue;
        set => BackgroundHex = WithRgb(BackgroundHex, AccentPalette.Join(value, BackgroundSaturation, BackgroundLightness));
    }

    public double BackgroundSaturation
    {
        get => AccentPalette.Split(Rgb(BackgroundHex)).Saturation;
        set => BackgroundHex = WithRgb(BackgroundHex, AccentPalette.Join(BackgroundHue, value, BackgroundLightness));
    }

    public double BackgroundLightness
    {
        get => AccentPalette.Split(Rgb(BackgroundHex)).Lightness;
        set => BackgroundHex = WithRgb(BackgroundHex, AccentPalette.Join(BackgroundHue, BackgroundSaturation, value));
    }

    /// <summary>
    /// Whether this swatch is the colour in force, compared on the three channels alone.
    /// </summary>
    /// <remarks>
    /// The alpha is deliberately not part of it: the stored default is <c>#CC000000</c> and the
    /// swatch is black, and those are the same colour at two opacities. Opacity has a slider of its
    /// own on this page, so choosing a colour must not silently move it.
    /// </remarks>
    private static bool Chosen(string swatch, string chosen) =>
        string.Equals(Rgb(swatch), Rgb(chosen), StringComparison.OrdinalIgnoreCase);

    private static string Rgb(string colour) =>
        colour.Length == 9 ? "#" + colour[3..] : colour;

    /// <summary>The colour in force with its three channels replaced, keeping whatever alpha it had.</summary>
    private static string WithRgb(string current, string rgb) =>
        current.Length == 9 ? current[..3] + rgb[1..] : rgb;

    public SubtitleStyle Style => _style;

    /// <summary>Takes a <c>#RRGGBB</c> and makes it the ink the subtitles are written in.</summary>
    public System.Windows.Input.ICommand ApplyForegroundCommand => _applyForeground;

    /// <summary>The same for the ground under them.</summary>
    public System.Windows.Input.ICommand ApplyBackgroundCommand => _applyBackground;

    public double FontSizePercent
    {
        get => _style.FontSizePercent;
        set => Update(Rebuild(fontSizePercent: value));
    }

    public string FontFamily
    {
        get => _style.FontFamily;
        set => Update(Rebuild(fontFamily: value));
    }

    public string ForegroundHex
    {
        get => _style.ForegroundHex;
        set => Update(Rebuild(foregroundHex: value));
    }

    public string BackgroundHex
    {
        get => _style.BackgroundHex;
        set => Update(Rebuild(backgroundHex: value));
    }

    public double BackgroundOpacity
    {
        get => _style.BackgroundOpacity;
        set => Update(Rebuild(backgroundOpacity: value));
    }

    public double OutlineThickness
    {
        get => _style.OutlineThickness;
        set => Update(Rebuild(outlineThickness: value));
    }

    public static double MinimumFontSizePercent => SubtitleStyle.MinimumFontSizePercent;

    public static double MaximumFontSizePercent => SubtitleStyle.MaximumFontSizePercent;

    public static double MaximumOutlineThickness => SubtitleStyle.MaximumOutlineThickness;

    /// <summary>Loads the stored style for the scope, or the engine default when nothing is stored.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAsync(_scope, _scopeKey, cancellationToken).ConfigureAwait(true);
        _isLoading = true;
        try
        {
            Update(stored?.SubtitleStyle ?? SubtitleStyle.EngineDefault);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Persists the current style without disturbing the other fields of the scope.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _repository.GetAsync(_scope, _scopeKey, cancellationToken).ConfigureAwait(true)
            ?? new PlaybackPreference { Scope = _scope, ScopeKey = _scopeKey };
        await _repository
            .SaveAsync(stored with { SubtitleStyle = _style }, cancellationToken)
            .ConfigureAwait(true);
    }

    /// <summary>Restores the engine default without deleting the rest of the preference.</summary>
    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        Update(SubtitleStyle.EngineDefault);
        return SaveAsync(cancellationToken);
    }

    private SubtitleStyle Rebuild(
        double? fontSizePercent = null,
        string? fontFamily = null,
        string? foregroundHex = null,
        string? backgroundHex = null,
        double? backgroundOpacity = null,
        double? outlineThickness = null) =>
        SubtitleStyle.Create(
            fontSizePercent ?? _style.FontSizePercent,
            fontFamily ?? _style.FontFamily,
            foregroundHex ?? _style.ForegroundHex,
            backgroundHex ?? _style.BackgroundHex,
            backgroundOpacity ?? _style.BackgroundOpacity,
            outlineThickness ?? _style.OutlineThickness);

    private void Update(SubtitleStyle style)
    {
        if (_style == style)
        {
            return;
        }

        _style = style;

        // A choice that dies with the window is not a choice. Every setter here goes through this,
        // so this is the one place it can be stored from — and it stores rather than waiting to be
        // asked, because nothing in the application was ever going to ask: walking the surface with
        // the mouse found four controls whose whole effect was a field of this object. Loading is
        // excluded because showing what is already stored is not a change worth storing back.
        if (!_isLoading)
        {
            _ = SaveAsync();
        }

        foreach (var name in new[]
        {
            nameof(Style),
            nameof(FontSizePercent),
            nameof(FontFamily),
            nameof(ForegroundHex),
            nameof(BackgroundHex),
            nameof(BackgroundOpacity),
            nameof(OutlineThickness),
            nameof(IsFirstForeground),
            nameof(IsSecondForeground),
            nameof(IsThirdForeground),
            nameof(IsFourthForeground),
            nameof(IsFifthForeground),
            nameof(IsSixthForeground),
            nameof(IsCustomForeground),
            nameof(IsFirstBackground),
            nameof(IsSecondBackground),
            nameof(IsThirdBackground),
            nameof(IsFourthBackground),
            nameof(IsFifthBackground),
            nameof(IsSixthBackground),
            nameof(IsCustomBackground),
            nameof(ForegroundHue),
            nameof(ForegroundSaturation),
            nameof(ForegroundLightness),
            nameof(BackgroundHue),
            nameof(BackgroundSaturation),
            nameof(BackgroundLightness),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>
    /// One swatch pressed. It takes the colour rather than an index, so the button carries the value
    /// it paints and nothing has to agree about the order of a list.
    /// </summary>
    private sealed class SwatchCommand(Action<string> apply) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) =>
            parameter is string value && SubtitleStyle.IsColour(value);

        public void Execute(object? parameter)
        {
            if (parameter is string value && CanExecute(value))
            {
                apply(value);
            }
        }
    }
}
