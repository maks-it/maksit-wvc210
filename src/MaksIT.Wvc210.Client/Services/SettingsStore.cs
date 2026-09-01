using System.Text.Json;
using MaksIT.Wvc210.Shared;

namespace MaksIT.Wvc210.Client;

/// <summary>
/// User-writable settings under AppData (<c>%AppData%/MaksIT/WVC210</c>).
/// The shipped <c>appsettings.json</c> next to the exe is seed-only and is never written.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MaksIT",
        "WVC210",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                loaded.Presets ??= [];
                loaded.UserHome ??= "";
                return loaded;
            }
        }
        catch
        {
            // Keep defaults if the file is missing or corrupt.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
