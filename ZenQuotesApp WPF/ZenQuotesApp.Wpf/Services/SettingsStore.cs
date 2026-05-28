using System.IO;
using System.Text.Json;

namespace ZenQuotesApp.Wpf.Services;

// Простое хранилище настроек в JSON-файле рядом с базой (LocalAppData).
public class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsStore(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    if (settings.BatchCount < 1)
                        settings.BatchCount = 1;
                    settings.ApiKey ??= string.Empty;
                    return settings;
                }
            }
        }
        catch
        {
            // Битый/нечитаемый файл — возвращаем настройки по умолчанию.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_path, json);
    }
}
