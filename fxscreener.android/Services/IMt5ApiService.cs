using fxscreener.android.Models;
using System.Text.Json.Serialization;

namespace fxscreener.android.Services;

/// <summary>
/// Интерфейс для работы с MT5 API
/// </summary>
public interface IMt5ApiService
{
    #region Управление сессией

    /// <summary>
    /// Подключиться к API с указанными настройками
    /// </summary>
    Task<bool> ConnectAsync(ApiSettings settings);

    /// <summary>
    /// Проверить, живо ли текущее соединение
    /// </summary>
    Task<bool> CheckConnectAsync();

    /// <summary>
    /// Отключиться от API
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Флаг: есть ли активное подключение
    /// </summary>
    bool IsConnected { get; }

    #endregion

    #region Работа с инструментами

    /// <summary>
    /// Получить параметры торгового инструмента
    /// </summary>
    /// <param name="symbol">Тикер (например, "EURUSD")</param>
    Task<SymbolParamsResponse?> GetSymbolParamsAsync(string symbol);

    #endregion

    #region Загрузка истории

    /// <summary>
    /// Загрузить исторические бары для нескольких инструментов одного периода
    /// </summary>
    /// <param name="cancellationToken">Токен отмены (для прогресса)</param>
    Task<PriceHistoryManyResponse?> GetPriceHistoryManyAsync(
        string operationId,
        List<string> symbols,
        DateTime from,
        DateTime to,
        int timeframeMinutes,
        CancellationToken cancellationToken = default);

    #endregion
}

#region Модели запросов/ответов API

/// <summary>
/// Запрос на подключение
/// </summary>
public class ConnectRequest
{
    public string id { get; set; } = string.Empty;
    public string login { get; set; } = string.Empty;
    public string password { get; set; } = string.Empty;
    public string host { get; set; } = string.Empty;
    public int port { get; set; } = 443;
}

/// <summary>
/// Запрос проверки соединения
/// </summary>
public class CheckConnectRequest
{
    public string id { get; set; } = string.Empty;
}

/// <summary>
/// Запрос отключения
/// </summary>
public class DisconnectRequest
{
    public string id { get; set; } = string.Empty;
}

/// <summary>
/// Ответ на Connect (может быть пустым или содержать статус)
/// </summary>
public class ConnectResponse
{
    public bool success { get; set; }
    public string? message { get; set; }
}

/// <summary>
/// Запрос параметров инструмента
/// </summary>
public class SymbolParamsRequest
{
    public string id { get; set; } = string.Empty;
    public string symbol { get; set; } = string.Empty;
}

/// <summary>
/// Ответ с параметрами инструмента
/// </summary>
public class SymbolParamsResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("symbolInfo")]
    public SymbolInfo SymbolInfo { get; set; } = new();

    [JsonPropertyName("symbolGroup")]
    public SymbolGroup SymbolGroup { get; set; } = new();
}

public class SymbolInfo
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("digits")]
    public int Digits { get; set; }

    [JsonPropertyName("point")]
    public double Point { get; set; }

    [JsonPropertyName("spread")]
    public double Spread { get; set; }

    [JsonPropertyName("tickValue")]
    public double TickValue { get; set; }

    [JsonPropertyName("tickSize")]
    public double TickSize { get; set; }

    [JsonPropertyName("contractSize")]
    public double ContractSize { get; set; }

    [JsonPropertyName("profitCurrency")]
    public string ProfitCurrency { get; set; } = string.Empty;

    [JsonPropertyName("marginCurrency")]
    public string MarginCurrency { get; set; } = string.Empty;

    [JsonPropertyName("precision")]
    public int Precision { get; set; }
}

public class SymbolGroup
{
    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;

    [JsonPropertyName("swapLong")]
    public double SwapLong { get; set; }

    [JsonPropertyName("swapShort")]
    public double SwapShort { get; set; }

    [JsonPropertyName("threeDaysSwap")]
    public string ThreeDaysSwap { get; set; } = string.Empty;

    [JsonPropertyName("minLots")]
    public double MinLots { get; set; }

    [JsonPropertyName("maxLots")]
    public double MaxLots { get; set; }

    [JsonPropertyName("lotsStep")]
    public double LotsStep { get; set; }
}

/// <summary>
/// Запрос истории для нескольких инструментов
/// </summary>
public class PriceHistoryManyRequest
{
    public string id { get; set; } = string.Empty;
    // Вместо списка объектов, API ожидает множественные параметры symbol
    public List<string> symbols { get; set; } = new();
    public DateTime? from { get; set; } // Начало периода
    public DateTime? to { get; set; }   // Конец периода
    public int timeFrame { get; set; }  // Таймфрейм в минутах
}

/// <summary>
/// Один элемент в массовом запросе
/// </summary>
public class SymbolPeriodRequest
{
    public string symbol { get; set; } = string.Empty;
    public int timeframe { get; set; }      // в минутах
    public int barsCount { get; set; } = 50;
}

/// <summary>
/// Ответ на массовый запрос истории
/// </summary>
public class PriceHistoryManyResponse : List<PriceHistoryItem>
{
}

/// <summary>
/// История для одного символа
/// </summary>
public class PriceHistoryItem
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("bars")]
    public List<PriceHistoryBar> Bars { get; set; } = new();
}

/// <summary>
/// Ответ на запрос истории для одного инструмента
/// </summary>
public class PriceHistoryResponse : List<PriceHistoryBar>
{
    // Наследуем от List<PriceHistoryBar>
}

/// <summary>
/// Данные одного бара (свечи)
/// </summary>
public class PriceHistoryBar
{
    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("openPrice")]
    public double OpenPrice { get; set; }

    [JsonPropertyName("highPrice")]
    public double HighPrice { get; set; }

    [JsonPropertyName("lowPrice")]
    public double LowPrice { get; set; }

    [JsonPropertyName("closePrice")]
    public double ClosePrice { get; set; }

    [JsonPropertyName("tickVolume")]
    public long TickVolume { get; set; }

    [JsonPropertyName("volume")]
    public long Volume { get; set; }

    [JsonPropertyName("spread")]
    public int Spread { get; set; }
}

#endregion