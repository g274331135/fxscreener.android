using System.Text.Json;

namespace fxscreener.android.Models;

/// <summary>
/// Настройки достройки баров
/// </summary>
public class BuildSettings
{
    private static readonly string SettingsFileName = "fxscreener_build.json";

    /// <summary>
    /// За сколько минут до закрытия начинать достройку (по умолчанию 5)
    /// </summary>
    public int BuildTimeMinutes { get; set; } = 5;

    /// <summary>
    /// Максимальное количество параллельных запросов к API
    /// </summary>
    public int MaxParallelRequests { get; set; } = 10;

    /// <summary>
    /// Сколько раз расширять период для получения 50 баров истории
    /// </summary>
    public int MaxHistoryAttempts { get; set; } = 3;

    /// <summary>
    /// Путь к файлу настроек
    /// </summary>
    private static string GetSettingsFilePath()
    {
        var appDataPath = FileSystem.AppDataDirectory;
        return Path.Combine(appDataPath, SettingsFileName);
    }

    public async Task SaveAsync()
    {
        try
        {
            var filePath = GetSettingsFilePath();
            var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving build settings: {ex.Message}");
        }
    }

    public static async Task<BuildSettings> LoadAsync()
    {
        try
        {
            var filePath = GetSettingsFilePath();
            if (!File.Exists(filePath))
                return new BuildSettings();

            var json = await File.ReadAllTextAsync(filePath);
            return System.Text.Json.JsonSerializer.Deserialize<BuildSettings>(json) ?? new BuildSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading build settings: {ex.Message}");
            return new BuildSettings();
        }
    }

    public static BuildSettings LoadSynchronous()
    {
        try
        {
            var filePath = GetSettingsFilePath();
            if (!File.Exists(filePath))
                return new BuildSettings();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<BuildSettings>(json) ?? new BuildSettings();
        }
        catch
        {
            return new BuildSettings();
        }
    }
}