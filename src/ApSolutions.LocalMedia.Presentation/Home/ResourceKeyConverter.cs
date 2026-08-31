// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using Avalonia.Data.Converters;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>
/// Turns a resource key into the text that key holds right now. Reason codes travel as keys so the
/// words follow the chosen language instead of being decided when the suggestion was computed.
/// </summary>
public sealed class ResourceKeyConverter : IValueConverter
{
    /// <summary>What joins several keys into the one sentence a reader hears.</summary>
    private const string Separator = ", ";

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;

        // A key in the parameter makes the value the thing to put INTO it: «Hemos encontrado {0}
        // carpetas más» is one sentence with a number in it, and the number is not a resource key.
        // StringFormat cannot do this here, because its format has to be a literal and the sentence
        // has to follow the chosen language.
        if (parameter is string format && !string.IsNullOrWhiteSpace(format))
        {
            return string.Format(culture, Resolve(format), value);
        }

        // A list as well as a key, because a help text is one string and an explanation is several
        // codes. Joining them here rather than in a view model is what keeps resource resolution -
        // which needs the application and its theme variant - out of the models.
        if (value is IEnumerable<string> keys)
        {
            return string.Join(Separator, keys.Select(Resolve));
        }

        return value is string key ? Resolve(key) : string.Empty;
    }

    private static string Resolve(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        var application = Avalonia.Application.Current;
        return application is not null
            && application.TryGetResource(key, application.ActualThemeVariant, out var resource)
                ? resource?.ToString() ?? key
                : key;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Resource keys are resolved in one direction only.");
}
