using System.Text.Json;
using System.Text;

namespace fxscreener.android.Models;

public class ApiSettings
{
    private static readonly string SettingsFileName = "fxscreener_settings.json";

    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Host { get; set; } = "mt5full2.mtapi.io";
    public int Port { get; set; } = 443;
    public string ApiKey { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public int UtcOffset { get; set; } = 3;

    /// <summary>
    /// Получить путь к файлу настроек в защищенном хранилище приложения
    /// </summary>
    private static string GetSettingsFilePath()
    {
        // ✅ Используем AppDataDirectory - не требует разрешений
        var appDataPath = FileSystem.AppDataDirectory;
        return Path.Combine(appDataPath, SettingsFileName);
    }

    public async Task SaveAsync()
    {
        try
        {
            var filePath = GetSettingsFilePath();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            System.Diagnostics.Debug.WriteLine($"Settings saved to: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    public static async Task<ApiSettings?> LoadAsync()
    {
        try
        {
            var filePath = GetSettingsFilePath();

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine("Settings file not found");
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var settings = JsonSerializer.Deserialize<ApiSettings>(json);

            System.Diagnostics.Debug.WriteLine($"Settings loaded from: {filePath}");
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            return null;
        }
    }

    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Login) &&
               !string.IsNullOrWhiteSpace(Password) &&
               !string.IsNullOrWhiteSpace(Host) &&
               Port > 0 && Port <= 65535 &&
               !string.IsNullOrWhiteSpace(ApiKey) &&
               !string.IsNullOrWhiteSpace(OperationId);
    }
}