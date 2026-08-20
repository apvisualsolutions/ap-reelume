// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Avalonia.Data.Converters;

namespace ApSolutions.LocalMedia.Presentation.Navigation;

/// <summary>What the converter answers about a destination.</summary>
public enum RouteStateKind
{
    /// <summary>A filled or hollow mark, so the current destination is not told by colour alone.</summary>
    Glyph,

    /// <summary>The status a screen reader announces for the destination that is open.</summary>
    Status,

    /// <summary>
    /// Whether this destination is the open one, for the bar that says so without using colour on its
    /// own. It answers a bool because the bar is <b>present or absent</b> rather than tinted: a
    /// dimmed bar would be a second thing to interpret, and absent is not the same as disabled.
    /// </summary>
    IsCurrent,
}

/// <summary>
/// Answers whether one navigation destination is the one currently open. The comparison lives here
/// rather than in five pairs of view-model properties, and it feeds both the visible mark and the
/// status a screen reader reads.
/// </summary>
public sealed class RouteStateConverter : IValueConverter
{
    public RouteStateKind Kind { get; set; } = RouteStateKind.Glyph;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = culture;
        var isCurrent = value is AppRoute current && parameter is AppRoute candidate && current == candidate;
        return Kind switch
        {
            RouteStateKind.Glyph => isCurrent ? "●" : "○",
            RouteStateKind.IsCurrent => isCurrent,
            _ => isCurrent ? ReadStatusText() : string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A destination state is derived, never written back.");

    /// <summary>The status text in the language in force, so the shell never holds a translation.</summary>
    private static string ReadStatusText()
    {
        const string Key = "NavigationCurrentStatus";
        var application = Avalonia.Application.Current;
        return application is not null
            && application.TryGetResource(Key, application.ActualThemeVariant, out var resource)
                ? resource?.ToString() ?? Key
                : Key;
    }
}
