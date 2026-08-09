namespace ApSolutions.LocalMedia.Application.Settings;

public interface ISettingsStore
{
    T? Read<T>(string key);

    void Write<T>(string key, T value);
}
