using System.Text;
using System.Text.Json;
using fxscreener.android.Models;

namespace fxscreener.android.Services;

/// <summary>
/// Реализация сервиса для работы с MT5 API
/// </summary>
public class Mt5ApiService : IMt5ApiService
{
    private readonly HttpClient _httpClient;
    private ApiSettings? _currentSettings;
    private bool _isConnected;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private System.Timers.Timer? _keepAliveTimer;

    public bool IsConnected => _isConnected;

    public Mt5ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://mt5full2.mtapi.io/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    #region Управление сессией

    public async Task<bool> ConnectAsync(ApiSettings settings)
    {
        await _connectLock.WaitAsync();
        try
        {
            var request = new ConnectRequest
            {
                id = settings.OperationId,
                login = settings.Login,
                password = settings.Password,
                host = settings.Host,
                port = settings.Port
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Устанавливаем api-key для этой сессии
            _httpClient.DefaultRequestHeaders.Remove("api-key");
            _httpClient.DefaultRequestHeaders.Add("api-key", settings.ApiKey);

            var response = await _httpClient.PostAsync("api/Connect", content);

            if (response.IsSuccessStatusCode)
            {
                _currentSettings = settings;
                _isConnected = true;

                // Запускаем проверку соединения каждые 2 минуты
                StartKeepAliveTimer();

                System.Diagnostics.Debug.WriteLine($"Connected successfully to {settings.Host}");
                return true;
            }

            var errorText = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Connect failed: {response.StatusCode} - {errorText}");

            _isConnected = false;
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Connect error: {ex.Message}");
            _isConnected = false;
            return false;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<bool> CheckConnectAsync()
    {
        if (!_isConnected || _currentSettings == null)
            return false;

        try
        {
            var request = new CheckConnectRequest
            {
                id = _currentSettings.OperationId
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("api/CheckConnect", content);

            _isConnected = response.IsSuccessStatusCode;

            if (!_isConnected)
            {
                System.Diagnostics.Debug.WriteLine("CheckConnect failed - session expired");
            }

            return _isConnected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CheckConnect error: {ex.Message}");
            _isConnected = false;
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!_isConnected || _currentSettings == null)
            return;

        try
        {
            var request = new DisconnectRequest
            {
                id = _currentSettings.OperationId
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync("api/Disconnect", content);

            System.Diagnostics.Debug.WriteLine("Disconnected successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex.Message}");
        }
        finally
        {
            _isConnected = false;
            _keepAliveTimer?.Stop();
            _keepAliveTimer?.Dispose();
            _keepAliveTimer = null;
        }
    }

    private void StartKeepAliveTimer()
    {
        _keepAliveTimer?.Stop();
        _keepAliveTimer?.Dispose();

        _keepAliveTimer = new System.Timers.Timer(TimeSpan.FromMinutes(2).TotalMilliseconds);
        _keepAliveTimer.Elapsed += async (s, e) => await KeepAliveAsync();
        _keepAliveTimer.Start();
    }

    private async Task KeepAliveAsync()
    {
        try
        {
            if (_isConnected)
            {
                await CheckConnectAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"KeepAlive error: {ex.Message}");
        }
    }

    #endregion

    #region Универсальный метод с проверкой подключения

    /// <summary>
    /// Универсальный метод для выполнения API-запросов с автоматическим переподключением
    /// </summary>
    private async Task<T?> ExecuteWithConnectCheckAsync<T>(
        Func<Task<HttpResponseMessage>> apiCall,
        CancellationToken cancellationToken = default)
    {
        // Сначала проверяем соединение
        if (!await EnsureConnectedAsync())
        {
            throw new InvalidOperationException("Нет подключения к MT5 API. Проверьте настройки.");
        }

        try
        {
            var response = await apiCall();

            // Если получили 401 Unauthorized - сессия истекла
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                System.Diagnostics.Debug.WriteLine("Got 401 Unauthorized, reconnecting...");
                _isConnected = false;

                // Пробуем переподключиться один раз
                if (await EnsureConnectedAsync())
                {
                    // Повторяем запрос
                    response = await apiCall();
                }
                else
                {
                    throw new InvalidOperationException("Не удалось восстановить сессию");
                }
            }

            // Проверяем другие ошибки
            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API error {response.StatusCode}: {errorText}");
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteWithConnectCheck error: {ex.Message}");
            throw;
        }
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        // Если соединение есть, проверяем что оно живо
        if (_isConnected && _currentSettings != null)
        {
            var isAlive = await CheckConnectAsync();
            if (isAlive) return true;
        }

        // Пробуем переподключиться, если есть настройки
        if (_currentSettings != null)
        {
            return await ConnectAsync(_currentSettings);
        }

        return false;
    }

    #endregion

    #region Работа с инструментами

    public async Task<SymbolParamsResponse?> GetSymbolParamsAsync(string symbol)
    {
        return await ExecuteWithConnectCheckAsync<SymbolParamsResponse>(async () =>
        {
            var request = new SymbolParamsRequest
            {
                id = _currentSettings!.OperationId,
                symbol = symbol
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _httpClient.PostAsync("api/SymbolParams", content);
        });
    }

    #endregion

    #region Загрузка истории

    public async Task<PriceHistoryManyResponse?> GetPriceHistoryManyAsync(
        PriceHistoryManyRequest request,
        CancellationToken cancellationToken = default)
    {
        // Убеждаемся, что используем правильный operationId
        if (_currentSettings != null)
        {
            request.id = _currentSettings.OperationId;
        }

        return await ExecuteWithConnectCheckAsync<PriceHistoryManyResponse>(async () =>
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            return await _httpClient.PostAsync("api/PriceHistoryMany", content, cancellationToken);
        }, cancellationToken);
    }

    public async Task<PriceHistoryResponse?> GetPriceHistoryAsync(
        string symbol,
        int timeframeMinutes,
        int barsCount = 50)
    {
        // Создаём массовый запрос для одного символа
        var manyRequest = new PriceHistoryManyRequest
        {
            id = _currentSettings?.OperationId ?? string.Empty,
            symbolsPeriods = new List<SymbolPeriodRequest>
            {
                new()
                {
                    symbol = symbol,
                    timeframe = timeframeMinutes,
                    barsCount = barsCount
                }
            }
        };

        var manyResponse = await GetPriceHistoryManyAsync(manyRequest);

        if (manyResponse?.data == null || manyResponse.data.Count == 0)
            return null;

        // Преобразуем в ответ для одного символа
        return new PriceHistoryResponse
        {
            symbol = symbol,
            bars = manyResponse.data[0].bars
        };
    }

    #endregion

    #region Вспомогательные методы

    /// <summary>
    /// Преобразовать строковый период (H1, D1) в минуты
    /// </summary>
    public static int ConvertPeriodToMinutes(string period)
    {
        return period.ToUpper() switch
        {
            "M1" => 1,
            "M5" => 5,
            "M15" => 15,
            "M30" => 30,
            "H1" => 60,
            "H4" => 240,
            "H6" => 360,
            "D1" => 1440,
            "W1" => 10080,
            "MN1" => 43200,
            _ => 60 // По умолчанию H1
        };
    }

    /// <summary>
    /// Преобразовать минуты в строковый период
    /// </summary>
    public static string ConvertMinutesToPeriod(int minutes)
    {
        return minutes switch
        {
            1 => "M1",
            5 => "M5",
            15 => "M15",
            30 => "M30",
            60 => "H1",
            240 => "H4",
            360 => "H6",
            1440 => "D1",
            10080 => "W1",
            43200 => "MN1",
            _ => "H1"
        };
    }

    #endregion
}