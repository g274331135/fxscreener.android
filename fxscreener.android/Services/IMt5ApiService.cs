using fxscreener.android.Models;

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
    /// <param name="request">Запрос с символами и периодом</param>
    /// <param name="cancellationToken">Токен отмены (для прогресса)</param>
    Task<PriceHistoryManyResponse?> GetPriceHistoryManyAsync(
        PriceHistoryManyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Загрузить исторические бары для одного инструмента
    /// </summary>
    /// <param name="symbol">Тикер</param>
    /// <param name="timeframeMinutes">Период в минутах (60 = H1, 1440 = D1)</param>
    /// <param name="barsCount">Количество баров (макс 1000)</param>
    Task<PriceHistoryResponse?> GetPriceHistoryAsync(
        string symbol,
        int timeframeMinutes,
        int barsCount = 50);

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
    public string symbol { get; set; } = string.Empty;
    public string description { get; set; } = string.Empty;
    public string currency { get; set; } = string.Empty;
    public double tickSize { get; set; }
    public double tickValue { get; set; }
    public int digits { get; set; }
    public double point { get; set; }
    public double spread { get; set; }
    public double spreadBalance { get; set; }
    public double contractSize { get; set; }
    public double profitCalcMode { get; set; }
    public double swapMode { get; set; }
    public double swapLong { get; set; }
    public double swapShort { get; set; }
    public double swap3Day { get; set; }
    public double swapRollover3Day { get; set; }
    public double swapRollover3DayFriday { get; set; }
    public double marginCurrency { get; set; }
    public double marginHedge { get; set; }
    public double marginMaintenance { get; set; }
    public double marginInitial { get; set; }

    // Торговые сессии можно добавить при необходимости
    // public List<TradingSession> sessions { get; set; }
}

/// <summary>
/// Запрос истории для нескольких инструментов
/// </summary>
public class PriceHistoryManyRequest
{
    public string id { get; set; } = string.Empty;
    public List<SymbolPeriodRequest> symbolsPeriods { get; set; } = new();
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
public class PriceHistoryManyResponse
{
    public List<SymbolHistory> data { get; set; } = new();
}

/// <summary>
/// История для одного символа
/// </summary>
public class SymbolHistory
{
    public string symbol { get; set; } = string.Empty;
    public List<BarData> bars { get; set; } = new();
}

/// <summary>
/// Ответ на запрос истории для одного инструмента
/// </summary>
public class PriceHistoryResponse
{
    public string symbol { get; set; } = string.Empty;
    public List<BarData> bars { get; set; } = new();
}

/// <summary>
/// Данные одного бара (свечи)
/// </summary>
public class BarData
{
    public DateTime time { get; set; }
    public double open { get; set; }
    public double high { get; set; }
    public double low { get; set; }
    public double close { get; set; }
    public long volume { get; set; }
    public int ticks { get; set; }

    // Для удобства работы в коде
    public bool IsBullish => close > open;
    public bool IsBearish => close < open;
    public double Range => high - low;
    public double Body => Math.Abs(close - open);
}

#endregion