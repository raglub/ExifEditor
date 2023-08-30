using System.IO;
using System.Text.Json;
using ExifEditor;

public class SettingsService
{
    private const string SettingsFileName = "settings.json";

    public static void SaveSettings(AppSettings settings)
    {
        string jsonString = JsonSerializer.Serialize(settings);
        File.WriteAllText(SettingsFileName, jsonString);
    }

    public static AppSettings LoadSettings()
    {
        if (File.Exists(SettingsFileName))
        {
            string jsonString = File.ReadAllText(SettingsFileName);
            var result = JsonSerializer.Deserialize<AppSettings>(jsonString);
            if (result is object) {
                return result;
            } else {
                new AppSettings();
            }
        }
        return new AppSettings();
    }
}