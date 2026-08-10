// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    public SubtitleStyleViewModel(
        IPlaybackPreferenceRepository repository,
        PreferenceScope scope = PreferenceScope.Global,
        string? scopeKey = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _scope = scope;
        _scopeKey = scopeKey ?? PlaybackPreference.GlobalKey;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Families that ship with Windows 11 and stay legible at every scaling.</summary>
    public static IReadOnlyList<string> SafeFontFamilies { get; } =
        ["Segoe UI", "Segoe UI Variable", "Arial", "Verdana", "Tahoma", "Consolas"];

    public SubtitleStyle Style => _style;

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
        Update(stored?.SubtitleStyle ?? SubtitleStyle.EngineDefault);
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
        foreach (var name in new[]
        {
            nameof(Style),
            nameof(FontSizePercent),
            nameof(FontFamily),
            nameof(ForegroundHex),
            nameof(BackgroundHex),
            nameof(BackgroundOpacity),
            nameof(OutlineThickness),
        })
        {
            OnPropertyChanged(name);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
