using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Trading212.Shared;
using Trading212.Shared.Models;
using Trading212.Shared.Services;

namespace Trading212McpServer;

[McpServerToolType]
public class Trading212Tools
{
    private readonly Trading212Client _client;
    private static readonly HttpClient _finnhubClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly string? _finnhubKey = Environment.GetEnvironmentVariable("FINNHUB_API_KEY");
    private static CacheService? _cacheInstance;
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    public Trading212Tools(Trading212Client client)
    {
        _client = client;
    }

    private async Task<CacheService> GetCacheAsync()
    {
        if (_cacheInstance is not null) return _cacheInstance;
        await _cacheLock.WaitAsync();
        try
        {
            if (_cacheInstance is not null) return _cacheInstance;
            var info = await _client.GetAccountInfoAsync();
            _cacheInstance = new CacheService(info.Id);
            return _cacheInstance;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    [McpServerTool(Name = "get_portfolio_summary")]
    [Description("Get a complete portfolio summary showing account value, cash, and all positions with P&L. " +
        "Includes ticker, name, quantity, average price, current price, current value, P&L, and P&L %. " +
        "Positions are sorted by current value descending.")]
    public async Task<string> GetPortfolioSummary()
    {
        try
        {
            var summary = await _client.GetPortfolioSummaryAsync();
            var cash = summary.Cash;
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("                    PORTFOLIO SUMMARY");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"  Total Account Value:  £{cash.Total:N2}");
            sb.AppendLine($"  Total Invested:       £{cash.Invested:N2}");
            sb.AppendLine($"  Total P&L:            £{cash.Result:N2} ({(cash.Invested != 0 ? cash.Result / cash.Invested * 100 : 0):N2}%)");
            sb.AppendLine($"  Free Cash:            £{cash.Free:N2}");
            sb.AppendLine($"  Pie Value:            £{cash.PieValue:N2}");
            sb.AppendLine();

            if (summary.Positions.Count == 0)
            {
                sb.AppendLine("  No open positions.");
                return sb.ToString();
            }

            var enriched = summary.Positions.Select(p =>
            {
                var w = p.WalletImpact;
                var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
                return new { Position = p, Name = p.Instrument.Name, CurrentValue = w.CurrentValue, TotalPl = w.UnrealizedProfitLoss, PlPct = plPct, TotalCost = w.TotalCost };
            })
            .OrderByDescending(x => x.CurrentValue)
            .ToList();

            sb.AppendLine($"  {"Ticker",-16} {"Name",-28} {"Qty",8} {"Cost £",10} {"Value £",12} {"P&L £",10} {"P&L %",8}");
            sb.AppendLine($"  {new string('─', 16)} {new string('─', 28)} {new string('─', 8)} {new string('─', 10)} {new string('─', 12)} {new string('─', 10)} {new string('─', 8)}");

            foreach (var item in enriched)
            {
                var p = item.Position;
                var name = item.Name.Length > 27 ? item.Name[..27] + "…" : item.Name;
                var plSign = item.TotalPl >= 0 ? "+" : "";
                sb.AppendLine($"  {p.Instrument.Ticker,-16} {name,-28} {p.Quantity,8:N2} {item.TotalCost,10:N2} {item.CurrentValue,12:N2} {plSign}{item.TotalPl,9:N2} {item.PlPct,7:N2}%");
            }

            sb.AppendLine();
            sb.AppendLine($"  Total positions: {summary.Positions.Count}");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching portfolio summary: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_position")]
    [Description("Get detailed information about a single position. " +
        "Trading 212 tickers use the format TICKER_EXCHANGE_EQ, e.g. TSLA_US_EQ, VOD_L_EQ, AAPL_US_EQ. " +
        "Use the search_instrument tool to find the exact ticker format.")]
    public async Task<string> GetPosition(
        [Description("Trading 212 ticker symbol, e.g. TSLA_US_EQ, VOD_L_EQ")] string ticker)
    {
        try
        {
            var p = await _client.GetPositionAsync(ticker);
            var w = p.WalletImpact;
            var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;

            var sb = new StringBuilder();
            sb.AppendLine($"═══ Position: {p.Instrument.Ticker} — {p.Instrument.Name} ═══");
            sb.AppendLine();
            sb.AppendLine($"  Quantity:            {p.Quantity:N4}");
            sb.AppendLine($"  Avg Price Paid:      {p.AveragePricePaid:N4} {p.Instrument.Currency}");
            sb.AppendLine($"  Current Price:       {p.CurrentPrice:N4} {p.Instrument.Currency}");
            sb.AppendLine();
            sb.AppendLine($"  Total Cost (GBP):    £{w.TotalCost:N2}");
            sb.AppendLine($"  Current Value (GBP): £{w.CurrentValue:N2}");
            sb.AppendLine();
            sb.AppendLine($"  Unrealised P&L:      £{w.UnrealizedProfitLoss:N2} ({plPct:N2}%)");
            sb.AppendLine($"  FX Impact:           £{w.FxImpact?.ToString("N2") ?? "N/A"}");
            sb.AppendLine();
            sb.AppendLine($"  First Buy Date:      {(p.CreatedAt.Length >= 10 ? p.CreatedAt[..10] : p.CreatedAt)}");

            return sb.ToString();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return $"Position '{ticker}' not found. Use search_instrument to find the correct ticker format (e.g. TSLA_US_EQ).";
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching position '{ticker}': {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_cash_balance")]
    [Description("Get account cash balance including total value, invested amount, P&L, free cash, and pie value.")]
    public async Task<string> GetCashBalance()
    {
        try
        {
            var cash = await _client.GetAccountCashAsync();

            var sb = new StringBuilder();
            sb.AppendLine("═══ Account Cash Balance ═══");
            sb.AppendLine();
            sb.AppendLine($"  Total Account Value:  £{cash.Total:N2}");
            sb.AppendLine($"  Total Invested:       £{cash.Invested:N2}");
            sb.AppendLine($"  Total P&L:            £{cash.Result:N2}");
            sb.AppendLine($"  Free Cash:            £{cash.Free:N2}");
            sb.AppendLine($"  Pie Value:            £{cash.PieValue:N2}");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching cash balance: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_portfolio_analysis")]
    [Description("Analyse the portfolio: top 5 winners and losers by P&L, position sizing as % of portfolio with " +
        "ASCII bar chart, concentration metrics (top 3 holdings %), positions under £1500, and cash allocation %.")]
    public async Task<string> GetPortfolioAnalysis()
    {
        try
        {
            var summary = await _client.GetPortfolioSummaryAsync();
            var cash = summary.Cash;
            var sb = new StringBuilder();

            if (summary.Positions.Count == 0)
            {
                sb.AppendLine("No positions to analyse.");
                return sb.ToString();
            }

            var enriched = summary.Positions.Select(p =>
            {
                var w = p.WalletImpact;
                var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
                return new { Position = p, Name = p.Instrument.Name, CurrentValue = w.CurrentValue, TotalPl = w.UnrealizedProfitLoss, PlPct = plPct, TotalCost = w.TotalCost };
            }).ToList();

            var totalPortfolioValue = cash.Total;

            // Top 5 winners
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("                   PORTFOLIO ANALYSIS");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("  ▲ TOP 5 WINNERS (by P&L)");
            sb.AppendLine($"  {"Ticker",-16} {"Name",-24} {"P&L £",10} {"P&L %",8}");
            sb.AppendLine($"  {new string('─', 16)} {new string('─', 24)} {new string('─', 10)} {new string('─', 8)}");
            foreach (var item in enriched.OrderByDescending(x => x.TotalPl).Take(5))
            {
                var name = item.Name.Length > 23 ? item.Name[..23] + "…" : item.Name;
                sb.AppendLine($"  {item.Position.Instrument.Ticker,-16} {name,-24} {item.TotalPl,10:N2} {item.PlPct,7:N2}%");
            }

            // Top 5 losers
            sb.AppendLine();
            sb.AppendLine("  ▼ TOP 5 LOSERS (by P&L)");
            sb.AppendLine($"  {"Ticker",-16} {"Name",-24} {"P&L £",10} {"P&L %",8}");
            sb.AppendLine($"  {new string('─', 16)} {new string('─', 24)} {new string('─', 10)} {new string('─', 8)}");
            foreach (var item in enriched.OrderBy(x => x.TotalPl).Take(5))
            {
                var name = item.Name.Length > 23 ? item.Name[..23] + "…" : item.Name;
                sb.AppendLine($"  {item.Position.Instrument.Ticker,-16} {name,-24} {item.TotalPl,10:N2} {item.PlPct,7:N2}%");
            }

            // Position sizing with bar chart
            sb.AppendLine();
            sb.AppendLine("  POSITION SIZING (% of portfolio)");
            sb.AppendLine($"  {"Ticker",-16} {"Value £",10} {"%",6}  Bar");
            sb.AppendLine($"  {new string('─', 16)} {new string('─', 10)} {new string('─', 6)}  {new string('─', 30)}");
            foreach (var item in enriched.OrderByDescending(x => x.CurrentValue))
            {
                var pct = totalPortfolioValue != 0 ? item.CurrentValue / totalPortfolioValue * 100 : 0;
                var barLen = (int)Math.Round((double)pct);
                var bar = new string('█', Math.Min(barLen, 30));
                sb.AppendLine($"  {item.Position.Instrument.Ticker,-16} {item.CurrentValue,10:N2} {pct,5:N1}%  {bar}");
            }

            // Concentration metrics
            sb.AppendLine();
            sb.AppendLine("  CONCENTRATION METRICS");
            var top3 = enriched.OrderByDescending(x => x.CurrentValue).Take(3).ToList();
            var top3Value = top3.Sum(x => x.CurrentValue);
            var top3Pct = totalPortfolioValue != 0 ? top3Value / totalPortfolioValue * 100 : 0;
            sb.AppendLine($"  Top 3 positions: £{top3Value:N2} ({top3Pct:N1}% of portfolio)");
            foreach (var item in top3)
            {
                var pct = totalPortfolioValue != 0 ? item.CurrentValue / totalPortfolioValue * 100 : 0;
                sb.AppendLine($"    - {item.Position.Instrument.Ticker}: £{item.CurrentValue:N2} ({pct:N1}%)");
            }

            // Positions under £1500
            sb.AppendLine();
            var smallPositions = enriched.Where(x => x.CurrentValue < 1500).OrderBy(x => x.CurrentValue).ToList();
            sb.AppendLine($"  Positions under £1,500: {smallPositions.Count} of {enriched.Count}");
            foreach (var item in smallPositions)
            {
                sb.AppendLine($"    - {item.Position.Instrument.Ticker}: £{item.CurrentValue:N2}");
            }

            // Cash allocation
            sb.AppendLine();
            var cashPct = totalPortfolioValue != 0 ? cash.Free / totalPortfolioValue * 100 : 0;
            sb.AppendLine($"  Cash Allocation: £{cash.Free:N2} ({cashPct:N1}% of total)");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching portfolio analysis: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_dividend_history")]
    [Description("Get recent dividend payments showing date, ticker, shares held, amount received, and type.")]
    public async Task<string> GetDividendHistory(
        [Description("Number of dividends to return (default 20, max 50)")] int limit = 20)
    {
        try
        {
            limit = Math.Clamp(limit, 1, 50);
            var dividends = await _client.GetDividendsAsync(limit);

            var sb = new StringBuilder();
            sb.AppendLine("═══ Dividend History ═══");
            sb.AppendLine();

            if (dividends.Items.Count == 0)
            {
                sb.AppendLine("  No dividends found.");
                return sb.ToString();
            }

            sb.AppendLine($"  {"Date",-12} {"Ticker",-16} {"Shares",8} {"Amount £",10} {"Type",-12}");
            sb.AppendLine($"  {new string('─', 12)} {new string('─', 16)} {new string('─', 8)} {new string('─', 10)} {new string('─', 12)}");

            decimal totalDividends = 0;
            foreach (var div in dividends.Items)
            {
                var date = div.PaidOn.Length >= 10 ? div.PaidOn[..10] : div.PaidOn;
                sb.AppendLine($"  {date,-12} {div.Ticker,-16} {div.Quantity,8:N2} {div.Amount,10:N2} {div.Type,-12}");
                totalDividends += div.Amount;
            }

            sb.AppendLine();
            sb.AppendLine($"  Total from {dividends.Items.Count} dividends: £{totalDividends:N2}");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching dividend history: {ex.Message}";
        }
    }

    [McpServerTool(Name = "search_instrument")]
    [Description("Search for Trading 212 instruments by name or ticker. Useful for finding the exact T212 ticker " +
        "format (e.g. searching 'Tesla' returns 'TSLA_US_EQ', searching 'Vodafone' returns 'VOD_L_EQ'). " +
        "Returns matching tickers with name, type, and currency.")]
    public async Task<string> SearchInstrument(
        [Description("Search term to match against instrument name or ticker (case-insensitive)")] string searchTerm)
    {
        try
        {
            var instruments = await _client.GetInstrumentsAsync();
            var term = searchTerm.Trim();

            var matches = instruments
                .Where(i => i.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                         || i.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase)
                         || i.Isin.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(i => i.Name)
                .Take(25)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"═══ Instrument Search: \"{searchTerm}\" ═══");
            sb.AppendLine();

            if (matches.Count == 0)
            {
                sb.AppendLine("  No instruments found matching that search term.");
                return sb.ToString();
            }

            sb.AppendLine($"  {"Ticker",-20} {"Name",-32} {"Type",-10} {"Currency",-6}");
            sb.AppendLine($"  {new string('─', 20)} {new string('─', 32)} {new string('─', 10)} {new string('─', 6)}");

            foreach (var inst in matches)
            {
                var name = inst.Name.Length > 31 ? inst.Name[..31] + "…" : inst.Name;
                sb.AppendLine($"  {inst.Ticker,-20} {name,-32} {inst.Type,-10} {inst.CurrencyCode,-6}");
            }

            sb.AppendLine();
            sb.AppendLine($"  {matches.Count} result(s) found" + (matches.Count == 25 ? " (showing first 25, refine your search)" : ""));

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error searching instruments: {ex.Message}";
        }
    }

    [McpServerTool(Name = "get_earnings_calendar")]
    [Description("Get upcoming earnings dates for all stocks in your portfolio. Shows next earnings date, " +
        "EPS estimates, and whether the company beat or missed estimates last quarter. " +
        "Requires FINNHUB_API_KEY environment variable to be set.")]
    public async Task<string> GetEarningsCalendar()
    {
        if (string.IsNullOrWhiteSpace(_finnhubKey))
            return "Earnings calendar unavailable — FINNHUB_API_KEY environment variable not set.";

        try
        {
            var summary = await _client.GetPortfolioSummaryAsync();
            var instruments = await _client.GetInstrumentsAsync();
            var typeMap = instruments.ToDictionary(i => i.Ticker, i => i.Type);

            var sb = new StringBuilder();
            sb.AppendLine("═══ Upcoming Earnings Calendar ═══");
            sb.AppendLine();
            sb.AppendLine($"  {"Ticker",-16} {"Next Earnings",-14} {"Days",5} {"Est EPS",9} {"Last EPS",10} {"Beat?",6}");
            sb.AppendLine($"  {new string('─', 16)} {new string('─', 14)} {new string('─', 5)} {new string('─', 9)} {new string('─', 10)} {new string('─', 6)}");

            var results = new List<(string Ticker, string Line, int DaysUntil)>();
            var now = DateTimeOffset.UtcNow;

            foreach (var p in summary.Positions)
            {
                if (typeMap.TryGetValue(p.Instrument.Ticker, out var t) && t == "ETF")
                    continue;

                var symbol = ToStandardSymbol(p.Instrument.Ticker, p.Instrument.Currency);

                try
                {
                    // Check LiteDB cache first
                    var cache = await GetCacheAsync();
                    var events = cache.GetEarningsIfFresh(symbol);

                    if (events is null)
                    {
                        var url = $"https://finnhub.io/api/v1/stock/earnings?symbol={symbol}&token={_finnhubKey}";
                        var json = await _finnhubClient.GetStringAsync(url);
                        events = JsonSerializer.Deserialize<List<EarningsEventData>>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
                        cache.UpsertEarnings(symbol, events);
                    }

                    if (events.Count == 0) continue;

                    var last = events.OrderByDescending(e => e.Period).FirstOrDefault(e => e.Actual is not null);
                    if (last is null) continue;

                    // Estimate next: last period + ~90 days
                    string nextDate = "—";
                    int daysUntil = 999;
                    if (last.Period is not null && DateTimeOffset.TryParse(last.Period, out var lastPeriod))
                    {
                        var nextEstimate = lastPeriod.AddDays(90);
                        if (nextEstimate < now) nextEstimate = lastPeriod.AddDays(180);
                        nextDate = nextEstimate.ToString("yyyy-MM-dd");
                        daysUntil = (int)(nextEstimate - now).TotalDays;
                    }

                    var estEps = last.Estimate?.ToString("N2") ?? "—";
                    var lastEps = last.Actual?.ToString("N2") ?? "—";
                    var beat = last.Actual is not null && last.Estimate is not null
                        ? (last.Actual > last.Estimate ? "Beat" : "Miss")
                        : "—";

                    results.Add((p.Instrument.Ticker, $"  {p.Instrument.Ticker,-16} {nextDate,-14} {daysUntil,5} {estEps,9} {lastEps,10} {beat,6}", daysUntil));
                }
                catch { /* skip individual failures */ }
            }

            foreach (var r in results.OrderBy(r => r.DaysUntil))
                sb.AppendLine(r.Line);

            sb.AppendLine();
            sb.AppendLine($"  {results.Count} stocks with earnings data.");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            return $"Error fetching earnings calendar: {ex.Message}";
        }
    }

    private static string ToStandardSymbol(string t212Ticker, string currency)
    {
        var parts = t212Ticker.Split('_');
        var symbol = parts[0];
        if (parts.Length >= 3)
        {
            return parts[1] switch
            {
                "US" => symbol,
                "L" => symbol + ".L",
                "DE" => symbol + ".DE",
                "PA" => symbol + ".PA",
                "MI" => symbol + ".MI",
                "AS" => symbol + ".AS",
                _ => symbol
            };
        }
        return currency switch
        {
            "GBX" or "GBP" => symbol + ".L",
            _ => symbol
        };
    }

}
