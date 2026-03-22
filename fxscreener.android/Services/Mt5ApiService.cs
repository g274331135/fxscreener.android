using fxscreener.android.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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

    private string _currentOperationId = string.Empty; // Храним текущий ID
    public bool IsConnected => _isConnected;

    public Mt5ApiService()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri("https://mt5full2.mtapi.io/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    #region Управление сессией

    /// <summary>
    /// Генерирует MD5 хеш для OperationId
    /// </summary>
    private string GenerateOperationId()
    {
        // Используем комбинацию логина + хоста + порта + случайное число
        var input = $"{_currentSettings?.Login}_{_currentSettings?.Host}_{_currentSettings?.Port}_{Guid.NewGuid()}";

        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        System.Diagnostics.Debug.WriteLine($"Generated OperationId: {hashString}");
        return hashString;
    }

    public async Task<bool> ConnectAsync(ApiSettings settings)
    {
        await _connectLock.WaitAsync();
        try
        {
            // Сохраняем настройки
            _currentSettings = settings;

            // Генерируем новый OperationId при каждом подключении
            _currentOperationId = GenerateOperationId();

            // Формируем URL с параметрами для GET-запроса
            var url = $"Connect?user={settings.Login}" +
                      $"&password={Uri.EscapeDataString(settings.Password)}" +
                      $"&host={settings.Host}" +
                      $"&port={settings.Port}" +
                      $"&id={_currentOperationId}" +
                      $"&connectTimeoutSeconds=30";

            // Устанавливаем api-key в заголовки
            _httpClient.DefaultRequestHeaders.Remove("ApiKey");
            _httpClient.DefaultRequestHeaders.Add("ApiKey", settings.ApiKey);

            System.Diagnostics.Debug.WriteLine($"Connecting with OperationId: {_currentOperationId}");

            // Используем GET
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                _isConnected = true;
                StartKeepAliveTimer();
                System.Diagnostics.Debug.WriteLine($"Connected successfully");
                return true;
            }

            var errorText = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"Connect failed: {response.StatusCode} - {errorText}");

            _isConnected = false;
            _currentOperationId = string.Empty;
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
        if (!_isConnected || string.IsNullOrEmpty(_currentOperationId))
            return false;

        try
        {
            var response = await _httpClient.GetAsync($"CheckConnect?id={_currentOperationId}");

            // Если получили 201 с сообщением "Client with id ... not found" — нужно переподключиться
            if ((int)response.StatusCode == 201)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                if (errorText.Contains("not found"))
                {
                    System.Diagnostics.Debug.WriteLine($"OperationId {_currentOperationId} not found, reconnecting...");
                    _isConnected = false;

                    // Пытаемся переподключиться
                    if (_currentSettings != null)
                    {
                        return await ConnectAsync(_currentSettings);
                    }
                    return false;
                }
            }

            _isConnected = response.IsSuccessStatusCode;
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
        if (!_isConnected || string.IsNullOrEmpty(_currentOperationId))
            return;

        try
        {
            await _httpClient.GetAsync($"Disconnect?id={_currentOperationId}");

            System.Diagnostics.Debug.WriteLine($"Disconnected OperationId: {_currentOperationId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex.Message}");
        }
        finally
        {
            _isConnected = false;
            _currentOperationId = string.Empty;
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

            // Обработка 401 Unauthorized
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                System.Diagnostics.Debug.WriteLine("Got 401 Unauthorized, reconnecting...");
                _isConnected = false;

                if (await EnsureConnectedAsync())
                {
                    response = await apiCall();
                }
                else
                {
                    throw new InvalidOperationException("Не удалось восстановить сессию");
                }
            }

            // Обработка 201 (Not Found для OperationId)
            if ((int)response.StatusCode == 201)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                if (errorText.Contains("not found"))
                {
                    System.Diagnostics.Debug.WriteLine("OperationId not found, reconnecting...");
                    _isConnected = false;

                    if (await EnsureConnectedAsync())
                    {
                        response = await apiCall();
                    }
                    else
                    {
                        throw new InvalidOperationException("Не удалось восстановить сессию");
                    }
                }
            }

            if (!response.IsSuccessStatusCode && (int)response.StatusCode != 201)
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
        if (_isConnected && !string.IsNullOrEmpty(_currentOperationId))
        {
            var isAlive = await CheckConnectAsync();
            if (isAlive) return true;
        }

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
        var url = $"SymbolParams?id={_currentOperationId}&symbol={Uri.EscapeDataString(symbol)}";

        return await ExecuteWithConnectCheckAsync<SymbolParamsResponse>(async () =>
        {
            return await _httpClient.GetAsync(url);
        });
    }

    #endregion

    #region Загрузка истории

    public async Task<PriceHistoryManyResponse?> GetPriceHistoryManyAsync(
        List<string> symbols,
        DateTime from,
        DateTime to,
        int timeframeMinutes,
        CancellationToken cancellationToken = default)
    {
        var url = $"PriceHistoryMany?id={_currentOperationId}";

        foreach (var symbol in symbols)
        {
            url += $"&symbol={Uri.EscapeDataString(symbol)}";
        }

        url += $"&from={from:yyyy-MM-ddTHH:mm:ss}";
        url += $"&to={to:yyyy-MM-ddTHH:mm:ss}";
        url += $"&timeFrame={timeframeMinutes}";

        return await ExecuteWithConnectCheckAsync<PriceHistoryManyResponse>(async () =>
        {
            return await _httpClient.GetAsync(url, cancellationToken);
        }, cancellationToken);
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