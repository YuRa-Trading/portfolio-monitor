using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Trading212McpServer.Models;

namespace Trading212McpServer;

public class Trading212Client
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Trading212Client> _logger;
    private List<Instrument>? _cachedInstruments;
    private DateTime _instrumentsCacheTime;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public Trading212Client(HttpClient httpClient, ILogger<Trading212Client> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private async Task<HttpResponseMessage> SendAsync(string endpoint)
    {
        _logger.LogInformation("API call: GET {Endpoint}", endpoint);
        var response = await _httpClient.GetAsync(endpoint);
        _logger.LogInformation("API response: {Endpoint} -> {StatusCode}", endpoint, (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("API error body: {Body}", body);
        }
        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<AccountCash> GetAccountCashAsync()
    {
        var response = await SendAsync("equity/account/cash");
        return await response.Content.ReadFromJsonAsync<AccountCash>()
            ?? throw new InvalidOperationException("Failed to deserialize account cash response.");
    }

    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        var response = await SendAsync("equity/account/info");
        return await response.Content.ReadFromJsonAsync<AccountInfo>()
            ?? throw new InvalidOperationException("Failed to deserialize account info response.");
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        var response = await SendAsync("equity/positions");
        return await response.Content.ReadFromJsonAsync<List<Position>>()
            ?? throw new InvalidOperationException("Failed to deserialize positions response.");
    }

    public async Task<Position> GetPositionAsync(string ticker)
    {
        var response = await SendAsync($"equity/positions/{Uri.EscapeDataString(ticker)}");
        return await response.Content.ReadFromJsonAsync<Position>()
            ?? throw new InvalidOperationException("Failed to deserialize position response.");
    }

    public async Task<List<Order>> GetOrdersAsync()
    {
        var response = await SendAsync("equity/orders");
        return await response.Content.ReadFromJsonAsync<List<Order>>()
            ?? throw new InvalidOperationException("Failed to deserialize orders response.");
    }

    public async Task<DividendResponse> GetDividendsAsync(int limit = 50)
    {
        var response = await SendAsync($"equity/history/dividends?limit={limit}");
        return await response.Content.ReadFromJsonAsync<DividendResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize dividends response.");
    }

    public async Task<OrderHistoryResponse> GetOrderHistoryAsync(int limit = 50)
    {
        var response = await SendAsync($"equity/history/orders?limit={limit}");
        return await response.Content.ReadFromJsonAsync<OrderHistoryResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize order history response.");
    }

    public async Task<List<Instrument>> GetInstrumentsAsync()
    {
        if (_cachedInstruments is not null && DateTime.UtcNow - _instrumentsCacheTime < CacheDuration)
        {
            _logger.LogInformation("Using cached instruments ({Count} instruments)", _cachedInstruments.Count);
            return _cachedInstruments;
        }

        var response = await SendAsync("equity/metadata/instruments");
        _cachedInstruments = await response.Content.ReadFromJsonAsync<List<Instrument>>()
            ?? throw new InvalidOperationException("Failed to deserialize instruments response.");
        _instrumentsCacheTime = DateTime.UtcNow;
        _logger.LogInformation("Cached {Count} instruments", _cachedInstruments.Count);
        return _cachedInstruments;
    }

    public async Task<TransactionResponse> GetTransactionsAsync(int limit = 50)
    {
        var response = await SendAsync($"equity/history/transactions?limit={limit}");
        return await response.Content.ReadFromJsonAsync<TransactionResponse>()
            ?? throw new InvalidOperationException("Failed to deserialize transactions response.");
    }

    public async Task<PortfolioSummary> GetPortfolioSummaryAsync()
    {
        _logger.LogInformation("Fetching portfolio summary (2 parallel calls)...");
        var cashTask = GetAccountCashAsync();
        var positionsTask = GetPositionsAsync();

        await Task.WhenAll(cashTask, positionsTask);

        _logger.LogInformation("Portfolio summary complete: {PositionCount} positions", (await positionsTask).Count);

        return new PortfolioSummary
        {
            Cash = await cashTask,
            Positions = await positionsTask
        };
    }
}
