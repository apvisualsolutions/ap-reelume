using System.Globalization;
using ApSolutions.LocalMedia.Application.Settings;

namespace ApSolutions.LocalMedia.Presentation.Language;

/// <summary>The two languages this application speaks; everything else resolves to Spanish.</summary>
public interface ILanguageService
{
    /// <summary>"es" or "en": the stored preference, or the system's language on first run.</summary>
    string Current { get; }

    /// <summary>Stores the preference and applies it to the running application.</summary>
    void Apply(string language);
}

/// <summary>
/// One source of truth for the language (BUG-011). The interface was pinned to Spanish while the
/// updater's summary and the TMDB metadata followed the machine's culture, so an English system
/// read release notes in one language inside a window speaking another. Now the resolved
/// preference sets the thread culture and the resource dictionaries together, and everything that
/// asks "which language?" gets one answer.
/// </summary>
public sealed class StoredLanguageService : ILanguageService
{
    public const string SettingKey = "ui.language";

    private readonly ISettingsStore _store;
    private readonly Avalonia.Application _application;

    public StoredLanguageService(ISettingsStore store, Avalonia.Application application)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _application = application ?? throw new ArgumentNullException(nameof(application));
    }

    public string Current => Resolve(_store.Read<string>(SettingKey));

    /// <summary>
    /// A stored choice wins; without one the application speaks Spanish, which is what it always
    /// declared. The fix here is coherence, not a new default: whatever is resolved is what the
    /// window, the update summary, and the metadata all use.
    /// </summary>
    public static string Resolve(string? stored) => stored is "es" or "en" ? stored : "es";

    /// <summary>Applies the resolved language without writing anything; the startup path.</summary>
    public void ApplyCurrent() => ApplyResolved(Current);

    public void Apply(string language)
    {
        if (language is not ("es" or "en"))
        {
            throw new ArgumentOutOfRangeException(nameof(language));
        }

        _store.Write(SettingKey, language);
        ApplyResolved(language);
    }

    private void ApplyResolved(string language)
    {
        var culture = CultureInfo.GetCultureInfo(language == "en" ? "en-US" : "es-ES");

        // The thread culture is what the updater's summary and the metadata language read; the
        // resource dictionaries are what the window shows. Setting them together is the fix: two
        // sources of truth is how the incoherence existed.
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        App.ApplyLanguage(_application, culture);
    }
}
