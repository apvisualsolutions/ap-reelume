// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Avalonia.Data.Converters;

namespace ApSolutions.LocalMedia.Presentation.Navigation;

/// <summary>What the converter answers about a destination.</summary>
/// <remarks>
/// There used to be a <c>Glyph</c> here, answering <c>●</c> or <c>○</c> beside each destination's
/// word. The rail is 64 px of pictograms now and has room for one mark, which is the one that says
/// <em>which</em> destination this is; what says it is <em>open</em> is the fill and the 3 px bar,
/// two signals with one of them not colour. The kind went with its last reader rather than staying
/// as an enum value nothing asks for.
/// </remarks>
public enum RouteStateKind
{
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
    public RouteStateKind Kind { get; set; } = RouteStateKind.Status;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = culture;
        var isCurrent = value is AppRoute current && parameter is AppRoute candidate && current == candidate;
        return Kind switch
        {
            RouteStateKind.IsCurrent => isCurrent,
            _ => isCurrent ? ReadStatusText() : string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("A destination state is derived, never written back.");

    /// <summary>The status text in the language in force, so the shell never holds a translation.</summary>
    /// <remarks>
    /// Three guards used to stand here — a null application, a missing resource and a null value —
    /// and <b>nothing in this repository could take any of them</b>: a converter only runs inside a
    /// running application, and the key is declared in both dictionaries with a gate that says so.
    /// They cost four branches that no test could reach and dragged this file's branch coverage down
    /// with them, which is the shape <c>eng/check-coverage.ps1</c> keeps finding. The answer to an
    /// unreachable guard is to remove it, not to write it an impossible test — and what is left says
    /// the true thing out loud: if the key is ever missing, the key itself is what appears on screen,
    /// which is a bug somebody can see rather than one this quietly hid.
    /// </remarks>
    private static string ReadStatusText()
    {
        const string Key = "NavigationCurrentStatus";
        var application = Avalonia.Application.Current!;
        _ = application.TryGetResource(Key, application.ActualThemeVariant, out var resource);
        return resource as string ?? Key;
    }
}
