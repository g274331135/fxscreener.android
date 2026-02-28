using System.Text.Json;
using System.Text;
using System.Security.Cryptography;

namespace fxscreener.android.Models;

/// <summary>
/// Настройки подключения к MT5 API
/// Хранятся во внешнем хранилище (Documents), что позволяет пережить удаление приложения
/// </summary>
public class ApiSettings
{
    private static readonly string SettingsFileName = "fxscreener_settings.json";

    // Основные параметры подключения
    public string Login { get; set; } = string.Empty;
    public string Host { get; set; } = "mt5full2.mtapi.io";
    public int Port { get; set; } = 443;
    public string ApiKey { get; set; } = string.Empty;
    public string OperationId { get; set; } = string.Empty;
    public int UtcOffset { get; set; } = 3; // UTC+3 по умолчанию (Москва)

    // Пароль храним зашифрованным (простая защита от "чужих глаз")
    private string _encryptedPassword = string.Empty;

    public string Password
    {
        get => DecryptPassword(_encryptedPassword);
        set => _encryptedPassword = EncryptPassword(value);
    }

    #region Шифрование пароля (простое)
    // ВНИМАНИЕ: Это простая защита от случайного просмотра файла.
    // Для настоящей безопасности нужно использовать Android Keystore.
    // Но для текущей задачи этого достаточно.

    private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("fxscreener_salt_2026");

    private string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

#if ANDROID
            // На Android используем ProtectedData (если API достаточно высокий)
            // Но для простоты оставим Base64 с "зашумлением"
            byte[] combined = new byte[plainBytes.Length + _entropy.Length];
            Buffer.BlockCopy(_entropy, 0, combined, 0, _entropy.Length);
            Buffer.BlockCopy(plainBytes, 0, combined, _entropy.Length, plainBytes.Length);

            return Convert.ToBase64String(combined);
#else
            // На других платформах можно использовать DPAPI, но нам нужен Android
            return Convert.ToBase64String(plainBytes);
#endif
        }
        catch
        {
            // В случае ошибки возвращаем пустую строку
            return string.Empty;
        }
    }

    private string DecryptPassword(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;

        try
        {
            byte[] combined = Convert.FromBase64String(cipherText);

#if ANDROID
            // Проверяем, что длина достаточна и "соль" совпадает
            if (combined.Length <= _entropy.Length)
                return string.Empty;

            // Извлекаем "соль" и проверяем (простая проверка целостности)
            byte[] entropyPart = new byte[_entropy.Length];
            Buffer.BlockCopy(combined, 0, entropyPart, 0, _entropy.Length);

            // Если соль не совпадает, значит файл повреждён или изменён
            if (!entropyPart.SequenceEqual(_entropy))
                return string.Empty;

            // Извлекаем реальные данные
            byte[] plainBytes = new byte[combined.Length - _entropy.Length];
            Buffer.BlockCopy(combined, _entropy.Length, plainBytes, 0, plainBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
#else
            return Encoding.UTF8.GetString(combined);
#endif
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion

    #region Работа с файловой системой

    /// <summary>
    /// Получить путь к файлу настроек во внешнем хранилище
    /// </summary>
    private static string GetSettingsFilePath()
    {
#if ANDROID
        try
        {
            // Android: /storage/emulated/0/Documents/fxscreener/
            var documentsPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDocuments)!.AbsolutePath;

            var appFolder = Path.Combine(documentsPath, "fxscreener");

            // Создаём папку, если её нет
            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            return Path.Combine(appFolder, SettingsFileName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting settings path: {ex.Message}");

            // Fallback: внутреннее хранилище (но тогда не переживёт удаление)
            var fallbackPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(fallbackPath, SettingsFileName);
        }
#else
        // Для других платформ (iOS, Windows) - другая логика
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, SettingsFileName);
#endif
    }

    /// <summary>
    /// Сохранить настройки в файл
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var filePath = GetSettingsFilePath();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(this, options);
            await File.WriteAllTextAsync(filePath, json);

            System.Diagnostics.Debug.WriteLine($"Settings saved to: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Загрузить настройки из файла
    /// </summary>
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

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var settings = JsonSerializer.Deserialize<ApiSettings>(json, options);

            System.Diagnostics.Debug.WriteLine($"Settings loaded from: {filePath}");
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Удалить файл настроек (например, при выходе из аккаунта)
    /// </summary>
    public static void DeleteSettingsFile()
    {
        try
        {
            var filePath = GetSettingsFilePath();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                System.Diagnostics.Debug.WriteLine("Settings file deleted");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error deleting settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Получить информацию о месте хранения (для отладки)
    /// </summary>
    public static string GetStorageInfo()
    {
        var path = GetSettingsFilePath();
        var exists = File.Exists(path);
        return $"Path: {path}\nExists: {exists}";
    }

    #endregion

    #region Валидация

    /// <summary>
    /// Проверить, заполнены ли обязательные поля
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Login) &&
               !string.IsNullOrWhiteSpace(Password) &&
               !string.IsNullOrWhiteSpace(Host) &&
               Port > 0 && Port <= 65535 &&
               !string.IsNullOrWhiteSpace(ApiKey) &&
               !string.IsNullOrWhiteSpace(OperationId);
    }

    #endregion
}