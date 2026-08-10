// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Presentation.Language;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ApSolutions.LocalMedia.UiTests.Settings;

/// <summary>
/// BUG-011: the window was pinned to Spanish while the update summary and the metadata followed
/// the machine's culture — two sources of truth for one question. The service is now the only
/// answer, and applying it moves the resources and the thread culture together.
/// </summary>
public sealed class LanguageServiceTests
{
    [Fact]
    public void Without_a_stored_choice_the_application_speaks_spanish_as_it_always_declared()
    {
        Assert.Equal("es", StoredLanguageService.Resolve(null));
        Assert.Equal("es", StoredLanguageService.Resolve("something-else"));
        Assert.Equal("en", StoredLanguageService.Resolve("en"));
        Assert.Equal("es", StoredLanguageService.Resolve("es"));
    }

    [AvaloniaFact]
    public void Applying_a_language_moves_the_resources_and_the_thread_culture_together()
    {
        Assert.NotNull(Avalonia.Application.Current);
        var application = Avalonia.Application.Current!;
        var store = new InMemoryStore();
        var service = new StoredLanguageService(store, application);
        var previousCulture = CultureInfo.CurrentUICulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            service.Apply("en");

            Assert.Equal("en", service.Current);
            Assert.Equal("en", store.Read<string>(StoredLanguageService.SettingKey));
            Assert.Equal("en-US", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("Language", Resource(application, "LanguageTitle"));

            service.Apply("es");

            Assert.Equal("es-ES", CultureInfo.CurrentUICulture.Name);
            Assert.Equal("Idioma", Resource(application, "LanguageTitle"));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Apply("fr"));
        }
        finally
        {
            // The application and the thread are shared with every other test in this process.
            ApSolutions.LocalMedia.Presentation.App.ApplyLanguage(application, CultureInfo.GetCultureInfo("es-ES"));
            CultureInfo.CurrentUICulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
        }
    }

    private static string? Resource(Avalonia.Application application, string key) =>
        application.TryGetResource(key, application.ActualThemeVariant, out var value)
            ? value?.ToString()
            : null;

    private sealed class InMemoryStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> _values = [];

        public T? Read<T>(string key) =>
            _values.TryGetValue(key, out var value) && value is T typed ? typed : default;

        public void Write<T>(string key, T value) => _values[key] = value;
    }
}
