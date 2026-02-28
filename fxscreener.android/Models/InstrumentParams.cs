using fxscreener.android.Services;
using System.Text.Json;

namespace fxscreener.android.Models;

/// <summary>
/// Параметры торгового инструмента, сохранённые из SymbolParams
/// </summary>
public class InstrumentParams
{
    #region Основные поля (из SymbolParams)

    public string Symbol { get; set; } = string.Empty;           // EURUSD
    public string Description { get; set; } = string.Empty;      // Euro vs US Dollar
    public string Currency { get; set; } = string.Empty;         // USD

    public double TickSize { get; set; }          // Минимальный шаг цены
    public double TickValue { get; set; }         // Стоимость тика
    public int Digits { get; set; }                // Количество знаков после запятой
    public double Point { get; set; }              // Размер пункта
    public double Spread { get; set; }             // Спред в пунктах
    public double ContractSize { get; set; }       // Размер контракта

    public double SwapLong { get; set; }           // Своп на длинную позицию
    public double SwapShort { get; set; }          // Своп на короткую позицию
    public double Swap3Day { get; set; }           // Своп за 3 дня (среда)

    #endregion

    #region Пользовательские настройки

    /// <summary>
    /// Период для сканирования (H1, H4, D1 и т.д.)
    /// </summary>
    public string Period { get; set; } = "H1";

    /// <summary>
    /// Активен ли инструмент для сканирования
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Дата последнего обновления параметров
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    #endregion

    #region Торговые сессии (можно расширить позже)

    // В будущем можно добавить часы торговли
    // public List<TradingSession> TradingSessions { get; set; } = new();

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Создать из ответа SymbolParams
    /// </summary>
    public static InstrumentParams FromSymbolParams(SymbolParamsResponse response, string period = "H1")
    {
        return new InstrumentParams
        {
            Symbol = response.symbol,
            Description = response.description ?? string.Empty,
            Currency = response.currency ?? string.Empty,
            TickSize = response.tickSize,
            TickValue = response.tickValue,
            Digits = response.digits,
            Point = response.point,
            Spread = response.spread,
            ContractSize = response.contractSize,
            SwapLong = response.swapLong,
            SwapShort = response.swapShort,
            Swap3Day = response.swap3Day,
            Period = period,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Для отображения в списке
    /// </summary>
    public string DisplayName => $"{Symbol} ({Period})";

    /// <summary>
    /// Ключ для словаря (Symbol_Period)
    /// </summary>
    public string Key => $"{Symbol}_{Period}";

    #endregion
}

/// <summary>
/// Хранилище списка инструментов (сохраняется в отдельный файл)
/// </summary>
public class InstrumentsStorage
{
    private static readonly string StorageFileName = "fxscreener_instruments.json";

    /// <summary>
    /// Словарь инструментов: Key = "EURUSD_H1"
    /// </summary>
    public Dictionary<string, InstrumentParams> Instruments { get; set; } = new();

    #region Сохранение/загрузка

    private static string GetStorageFilePath()
    {
#if ANDROID
        try
        {
            // Используем ту же папку Documents/fxscreener что и для настроек
            var documentsPath = Android.OS.Environment.GetExternalStoragePublicDirectory(
                Android.OS.Environment.DirectoryDocuments)!.AbsolutePath;

            var appFolder = Path.Combine(documentsPath, "fxscreener");

            if (!Directory.Exists(appFolder))
                Directory.CreateDirectory(appFolder);

            return Path.Combine(appFolder, StorageFileName);
        }
        catch
        {
            var fallbackPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(fallbackPath, StorageFileName);
        }
#else
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, StorageFileName);
#endif
    }

    /// <summary>
    /// Сохранить список инструментов
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var filePath = GetStorageFilePath();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            var json = JsonSerializer.Serialize(this, options);
            await File.WriteAllTextAsync(filePath, json);

            System.Diagnostics.Debug.WriteLine($"Instruments saved to: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving instruments: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Загрузить список инструментов
    /// </summary>
    public static async Task<InstrumentsStorage> LoadAsync()
    {
        try
        {
            var filePath = GetStorageFilePath();

            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine("Instruments file not found, creating new storage");
                return new InstrumentsStorage();
            }

            var json = await File.ReadAllTextAsync(filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var storage = JsonSerializer.Deserialize<InstrumentsStorage>(json, options);

            System.Diagnostics.Debug.WriteLine($"Instruments loaded from: {filePath}");

            return storage ?? new InstrumentsStorage();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading instruments: {ex.Message}");
            return new InstrumentsStorage();
        }
    }

    #endregion

    #region Работа со списком

    /// <summary>
    /// Добавить или обновить инструмент
    /// </summary>
    public void AddOrUpdate(InstrumentParams instrument)
    {
        Instruments[instrument.Key] = instrument;
    }

    /// <summary>
    /// Удалить инструмент
    /// </summary>
    public bool Remove(string symbol, string period)
    {
        var key = $"{symbol}_{period}";
        return Instruments.Remove(key);
    }

    /// <summary>
    /// Удалить инструмент
    /// </summary>
    public bool Remove(InstrumentParams instrument)
    {
        return Remove(instrument.Symbol, instrument.Period);
    }

    /// <summary>
    /// Получить все активные инструменты
    /// </summary>
    public List<InstrumentParams> GetActiveInstruments()
    {
        return Instruments.Values.Where(i => i.IsActive).ToList();
    }

    /// <summary>
    /// Получить все инструменты (в виде списка)
    /// </summary>
    public List<InstrumentParams> GetAllInstruments()
    {
        return Instruments.Values.ToList();
    }

    /// <summary>
    /// Проверить, существует ли инструмент
    /// </summary>
    public bool Exists(string symbol, string period)
    {
        var key = $"{symbol}_{period}";
        return Instruments.ContainsKey(key);
    }

    /// <summary>
    /// Получить инструмент по ключу
    /// </summary>
    public InstrumentParams? Get(string symbol, string period)
    {
        var key = $"{symbol}_{period}";
        return Instruments.TryGetValue(key, out var instrument) ? instrument : null;
    }

    #endregion
}