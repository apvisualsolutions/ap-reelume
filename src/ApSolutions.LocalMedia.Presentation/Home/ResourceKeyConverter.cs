using System.Globalization;
using Avalonia.Data.Converters;

namespace ApSolutions.LocalMedia.Presentation.Home;

/// <summary>
/// Turns a resource key into the text that key holds right now. Reason codes travel as keys so the
/// words follow the chosen language instead of being decided when the suggestion was computed.
/// </summary>
public sealed class ResourceKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        if (value is not string key || string.IsNullOrWhiteSpace(key))
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
