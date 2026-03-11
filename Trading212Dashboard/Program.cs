using System.ServiceModel.Syndication;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using Trading212McpServer;
using Trading212McpServer.Config;
using Trading212McpServer.Models;

var apiKey = Environment.GetEnvironmentVariable("T212_API_KEY");
var apiSecret = Environment.GetEnvironmentVariable("T212_API_SECRET");
var environment = Environment.GetEnvironmentVariable("T212_ENVIRONMENT") ?? "demo";

if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
{
    Console.Error.WriteLine("ERROR: Missing T212_API_KEY / T212_API_SECRET environment variables.");
    Environment.Exit(1);
}

var baseUrl = environment.Equals("live", StringComparison.OrdinalIgnoreCase)
    ? "https://live.trading212.com/api/v0"
    : "https://demo.trading212.com/api/v0";

var credentials = Convert.ToBase64String(
    System.Text.Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<Trading212Client>(client =>
{
    client.BaseAddress = new Uri(baseUrl + "/");
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// Initialize alert config from appsettings.json
AlertConfigLoader.Load(builder.Configuration);

var app = builder.Build();

app.UseCors();
app.UseStaticFiles();

// ─── Banner ──────────────────────────────────────────────────────
var envLabel = environment.Equals("live", StringComparison.OrdinalIgnoreCase) ? "LIVE" : "DEMO";
Console.WriteLine();
Console.WriteLine("  ╔══════════════════════════════════════════╗");
Console.WriteLine("  ║   YuRa Trading · Portfolio Dashboard     ║");
Console.WriteLine($"  ║   Environment: {envLabel,-4}  ·  Port 5050        ║");
Console.WriteLine("  ║   http://localhost:5050                  ║");
Console.WriteLine("  ╚══════════════════════════════════════════╝");
Console.WriteLine();

// ─── Portfolio cache (avoids duplicate T212 API calls) ───────────
PortfolioSummary? cachedSummary = null;
DateTimeOffset cacheTime = DateTimeOffset.MinValue;
var cacheLock = new SemaphoreSlim(1, 1);
const int CacheSeconds = 15;

// ─── Instruments cache (avoids duplicate T212 instrument API calls) ───
Dictionary<string, string> cachedTypeMap = [];
DateTimeOffset instrumentsCacheTime = DateTimeOffset.MinValue;
var instrumentsCacheLock = new SemaphoreSlim(1, 1);

async Task<Dictionary<string, string>> GetCachedTypeMap(Trading212Client client)
{
    if (cachedTypeMap.Count > 0 && DateTimeOffset.UtcNow - instrumentsCacheTime < TimeSpan.FromHours(1))
        return cachedTypeMap;

    await instrumentsCacheLock.WaitAsync();
    try
    {
        if (cachedTypeMap.Count > 0 && DateTimeOffset.UtcNow - instrumentsCacheTime < TimeSpan.FromHours(1))
            return cachedTypeMap;

        var instruments = await client.GetInstrumentsAsync();
        var map = new Dictionary<string, string>();
        foreach (var inst in instruments)
            map[inst.Ticker] = inst.Type;
        cachedTypeMap = map;
        instrumentsCacheTime = DateTimeOffset.UtcNow;
        return cachedTypeMap;
    }
    catch
    {
        return cachedTypeMap; // return stale cache if available
    }
    finally
    {
        instrumentsCacheLock.Release();
    }
}

async Task<PortfolioSummary> GetCachedSummary(Trading212Client client)
{
    if (cachedSummary is not null && DateTimeOffset.UtcNow - cacheTime < TimeSpan.FromSeconds(CacheSeconds))
        return cachedSummary;

    await cacheLock.WaitAsync();
    try
    {
        if (cachedSummary is not null && DateTimeOffset.UtcNow - cacheTime < TimeSpan.FromSeconds(CacheSeconds))
            return cachedSummary;

        cachedSummary = await client.GetPortfolioSummaryAsync();
        cacheTime = DateTimeOffset.UtcNow;

        // Auto-save daily snapshot
        _ = Task.Run(() => SaveSnapshotIfNeeded(cachedSummary));

        return cachedSummary;
    }
    finally
    {
        cacheLock.Release();
    }
}

// ─── Portfolio snapshots (daily history) ────────────────────────
var snapshotPath = Path.Combine(AppContext.BaseDirectory, "portfolio-snapshots.json");
var snapshotLock = new SemaphoreSlim(1, 1);

async Task SaveSnapshotIfNeeded(PortfolioSummary summary)
{
    await snapshotLock.WaitAsync();
    try
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var snapshots = new List<Dictionary<string, object>>();

        if (File.Exists(snapshotPath))
        {
            var json = await File.ReadAllTextAsync(snapshotPath);
            snapshots = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];
        }

        if (snapshots.Any(s => s.TryGetValue("date", out var d) && d?.ToString() == today))
            return;

        var invested = summary.Cash.Invested;
        var pnlPct = invested != 0 ? Math.Round(summary.Cash.Result / invested * 100, 2) : 0;

        snapshots.Add(new Dictionary<string, object>
        {
            ["date"] = today,
            ["totalValue"] = summary.Cash.Total,
            ["invested"] = invested,
            ["pnl"] = summary.Cash.Result,
            ["pnlPct"] = pnlPct,
            ["freeCash"] = summary.Cash.Free,
            ["positionCount"] = summary.Positions.Count
        });

        if (snapshots.Count > 365)
            snapshots = snapshots.Skip(snapshots.Count - 365).ToList();

        await File.WriteAllTextAsync(snapshotPath, JsonSerializer.Serialize(snapshots));
    }
    catch { /* don't fail portfolio fetch due to snapshot error */ }
    finally
    {
        snapshotLock.Release();
    }
}

// ─── Endpoints ───────────────────────────────────────────────────

app.MapGet("/api/portfolio", async (Trading212Client client) =>
{
    var summary = await GetCachedSummary(client);
    var totalValue = summary.Cash.Total;

    // Fetch instrument types (app-level cache)
    var typeMap = await GetCachedTypeMap(client);

    var positions = summary.Positions.Select(p =>
    {
        var w = p.WalletImpact;
        var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
        // Parse exchange from ticker: TSLA_US_EQ (3-part) or EZJl_EQ (2-part UK)
        var parts = p.Instrument.Ticker.Split('_');
        var exchange = parts.Length >= 3 ? parts[1] :
            p.Instrument.Currency is "GBX" or "GBP" ? "L" :
            p.Instrument.Currency == "EUR" ? "EU" : "?";
        // Parse holding period
        DateTimeOffset? boughtAt = null;
        if (DateTimeOffset.TryParse(p.CreatedAt, out var parsed)) boughtAt = parsed;
        // Instrument type
        var instrumentType = typeMap.TryGetValue(p.Instrument.Ticker, out var t) ? t : "UNKNOWN";

        return new
        {
            ticker = p.Instrument.Ticker,
            name = p.Instrument.Name,
            quantity = p.Quantity,
            averagePrice = p.AveragePricePaid,
            currentPrice = p.CurrentPrice,
            investedValue = w.TotalCost,
            currentValue = w.CurrentValue,
            profitLoss = w.UnrealizedProfitLoss,
            profitLossPercent = Math.Round(plPct, 2),
            currency = p.Instrument.Currency,
            weight = totalValue != 0 ? Math.Round(w.CurrentValue / totalValue * 100, 2) : 0,
            exchange,
            boughtAt = boughtAt?.ToString("yyyy-MM-dd"),
            type = instrumentType
        };
    })
    .OrderByDescending(x => x.currentValue)
    .ToList();

    return Results.Json(new
    {
        positions,
        cash = new
        {
            free = summary.Cash.Free,
            invested = summary.Cash.Invested,
            total = summary.Cash.Total,
            result = summary.Cash.Result,
            pieValue = summary.Cash.PieValue
        },
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/cash", async (Trading212Client client) =>
{
    var cash = await client.GetAccountCashAsync();
    return Results.Json(new
    {
        free = cash.Free,
        invested = cash.Invested,
        total = cash.Total,
        result = cash.Result,
        pieValue = cash.PieValue
    });
});

app.MapGet("/api/alerts", async (Trading212Client client) =>
{
    var config = AlertConfigLoader.Load();
    var summary = await GetCachedSummary(client);
    var cash = summary.Cash;
    var totalValue = cash.Total;
    var alerts = new List<object>();

    var enriched = summary.Positions.Select(p =>
    {
        var w = p.WalletImpact;
        var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
        return new
        {
            ticker = p.Instrument.Ticker,
            name = p.Instrument.Name,
            currentValue = w.CurrentValue,
            plPct
        };
    }).ToList();

    foreach (var pos in enriched)
    {
        if (pos.plPct < -config.Portfolio.MaxDrawdownPercent)
        {
            alerts.Add(new
            {
                severity = "HIGH",
                type = "drawdown",
                ticker = pos.ticker,
                name = pos.name,
                message = $"Down {Math.Abs(pos.plPct):N1}% — exceeds {config.Portfolio.MaxDrawdownPercent}% threshold",
                value = Math.Round(pos.plPct, 1)
            });
        }

        if (totalValue != 0)
        {
            var concentration = pos.currentValue / totalValue * 100;
            if (concentration > config.Portfolio.MaxConcentrationPercent)
            {
                alerts.Add(new
                {
                    severity = "WARN",
                    type = "concentration",
                    ticker = pos.ticker,
                    name = pos.name,
                    message = $"{concentration:N1}% of portfolio — exceeds {config.Portfolio.MaxConcentrationPercent}% limit",
                    value = Math.Round(concentration, 1)
                });
            }
        }

        if (pos.currentValue < config.Portfolio.MinPositionSize)
        {
            alerts.Add(new
            {
                severity = "SIZE",
                type = "undersize",
                ticker = pos.ticker,
                name = pos.name,
                message = $"£{pos.currentValue:N0} — below £{config.Portfolio.MinPositionSize:N0} minimum",
                value = pos.currentValue
            });
        }
    }

    if (enriched.Count > config.Portfolio.MaxPositions)
    {
        alerts.Add(new
        {
            severity = "WARN",
            type = "count",
            ticker = "",
            name = "Position Count",
            message = $"{enriched.Count} positions — exceeds {config.Portfolio.MaxPositions} maximum",
            value = (decimal)enriched.Count
        });
    }

    if (totalValue != 0)
    {
        var cashPct = cash.Free / totalValue * 100;
        if (cashPct > 30)
        {
            alerts.Add(new
            {
                severity = "WARN",
                type = "cash",
                ticker = "",
                name = "Cash Allocation",
                message = $"{cashPct:N1}% (£{cash.Free:N0}) — exceeds 30% threshold",
                value = Math.Round(cashPct, 1)
            });
        }
    }

    return Results.Json(new
    {
        alerts,
        thresholds = new
        {
            maxDrawdownPercent = config.Portfolio.MaxDrawdownPercent,
            maxConcentrationPercent = config.Portfolio.MaxConcentrationPercent,
            minPositionSize = config.Portfolio.MinPositionSize,
            maxPositions = config.Portfolio.MaxPositions
        },
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/news", async (int? limit) =>
{
    var config = AlertConfigLoader.Load();
    var maxItems = Math.Clamp(limit ?? 8, 1, 30);
    var cutoff = DateTimeOffset.UtcNow.AddHours(-48);
    var scored = new List<object>();

    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; T212Dashboard/1.0)");
    httpClient.Timeout = TimeSpan.FromSeconds(8);

    var feedsToFetch = config.NewsFeeds.Take(3).ToList();

    foreach (var feedUrl in feedsToFetch)
    {
        try
        {
            using var stream = await httpClient.GetStreamAsync(feedUrl);
            using var reader = XmlReader.Create(stream);
            var feed = SyndicationFeed.Load(reader);
            var sourceName = feed.Title?.Text ?? new Uri(feedUrl).Host;

            foreach (var entry in feed.Items)
            {
                var published = entry.PublishDate != DateTimeOffset.MinValue
                    ? entry.PublishDate
                    : entry.LastUpdatedTime;

                if (published < cutoff) continue;

                var title = entry.Title?.Text ?? "(no title)";
                var rawSummary = entry.Summary?.Text ?? "";
                var summary = StripHtml(rawSummary);
                if (summary.Length > 200) summary = summary[..200] + "…";
                var link = entry.Links.FirstOrDefault()?.Uri?.AbsoluteUri ?? "";

                var text = $"{title} {summary}";
                var relevance = config.NewsKeywords.Count(k =>
                    text.Contains(k, StringComparison.OrdinalIgnoreCase));

                if (relevance > 0)
                {
                    scored.Add(new
                    {
                        title,
                        summary,
                        link,
                        date = published,
                        source = sourceName,
                        relevance
                    });
                }
            }
        }
        catch
        {
            // Skip failing feeds
        }
    }

    var results = scored
        .OrderByDescending(x => ((dynamic)x).relevance)
        .ThenByDescending(x => (DateTimeOffset)((dynamic)x).date)
        .Take(maxItems)
        .ToList();

    return Results.Json(results);
});

app.MapGet("/api/dividends", async (Trading212Client client, int? limit) =>
{
    var maxItems = Math.Clamp(limit ?? 20, 1, 50);
    var dividends = await client.GetDividendsAsync(maxItems);

    var items = dividends.Items.Select(d => new
    {
        ticker = d.Ticker,
        amount = d.Amount,
        quantity = d.Quantity,
        type = d.Type,
        paidOn = d.PaidOn.Length >= 10 ? d.PaidOn[..10] : d.PaidOn
    }).ToList();

    var totalIncome = dividends.Items.Sum(d => d.Amount);

    return Results.Json(new
    {
        items,
        totalIncome,
        count = items.Count
    });
});

app.MapGet("/api/analytics", async (Trading212Client client) =>
{
    var config = AlertConfigLoader.Load();
    var summary = await GetCachedSummary(client);
    var cash = summary.Cash;
    var totalValue = cash.Total;

    var enriched = summary.Positions.Select(p =>
    {
        var w = p.WalletImpact;
        var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
        var parts = p.Instrument.Ticker.Split('_');
        // 3-part: TSLA_US_EQ → exchange=US; 2-part: EZJl_EQ → exchange=L (UK, inferred from currency)
        var exchange = parts.Length >= 3 ? parts[1] :
            p.Instrument.Currency is "GBX" or "GBP" ? "L" :
            p.Instrument.Currency == "EUR" ? "EU" : "?";

        return new
        {
            ticker = p.Instrument.Ticker,
            name = p.Instrument.Name,
            currentValue = w.CurrentValue,
            investedValue = w.TotalCost,
            profitLoss = w.UnrealizedProfitLoss,
            plPct = Math.Round(plPct, 2),
            currency = p.Instrument.Currency,
            exchange,
            weight = totalValue != 0 ? Math.Round(w.CurrentValue / totalValue * 100, 2) : 0
        };
    }).ToList();

    // Exchange breakdown
    var byExchange = enriched
        .GroupBy(p => p.exchange switch
        {
            "US" => "US",
            "L" => "UK",
            "DE" or "MI" or "PA" or "AS" or "EU" => "EU",
            _ => p.exchange
        })
        .Select(g => new { region = g.Key, value = g.Sum(p => p.currentValue), count = g.Count(),
            pct = totalValue != 0 ? Math.Round(g.Sum(p => p.currentValue) / totalValue * 100, 1) : 0 })
        .OrderByDescending(x => x.value).ToList();

    // Currency exposure
    var byCurrency = enriched
        .GroupBy(p => p.currency)
        .Select(g => new { currency = g.Key, value = g.Sum(p => p.currentValue), count = g.Count(),
            pct = totalValue != 0 ? Math.Round(g.Sum(p => p.currentValue) / totalValue * 100, 1) : 0 })
        .OrderByDescending(x => x.value).ToList();

    // Best & worst 3
    var best3 = enriched.OrderByDescending(x => x.plPct).Take(3)
        .Select(p => new { p.ticker, p.name, p.plPct, p.profitLoss }).ToList();
    var worst3 = enriched.OrderBy(x => x.plPct).Take(3)
        .Select(p => new { p.ticker, p.name, p.plPct, p.profitLoss }).ToList();

    // Top 3 concentration
    var top3 = enriched.OrderByDescending(x => x.currentValue).Take(3)
        .Select(p => new { p.ticker, p.name, p.currentValue, p.weight }).ToList();
    var top3Pct = totalValue != 0 ? Math.Round(top3.Sum(p => p.currentValue) / totalValue * 100, 1) : 0;

    // Small positions
    var small = enriched.Where(p => p.currentValue < config.Portfolio.MinPositionSize)
        .OrderBy(p => p.currentValue)
        .Select(p => new { p.ticker, p.name, p.currentValue }).ToList();

    // Invested vs current per position (for bar comparison)
    var comparison = enriched.OrderByDescending(x => x.currentValue)
        .Select(p => new { p.ticker, p.name, p.investedValue, p.currentValue, p.profitLoss, p.plPct }).ToList();

    // Type breakdown (STOCK vs ETF) — uses app-level cached type map
    var typeMapAnalytics = await GetCachedTypeMap(client);

    var byType = enriched
        .GroupBy(p => typeMapAnalytics.TryGetValue(p.ticker, out var t) ? t : "UNKNOWN")
        .Select(g => new { type = g.Key, value = g.Sum(p => p.currentValue), count = g.Count(),
            pct = totalValue != 0 ? Math.Round(g.Sum(p => p.currentValue) / totalValue * 100, 1) : 0 })
        .OrderByDescending(x => x.value).ToList();

    return Results.Json(new
    {
        byExchange,
        byCurrency,
        byType,
        best3,
        worst3,
        top3,
        top3Pct,
        small,
        smallThreshold = config.Portfolio.MinPositionSize,
        comparison,
        positionCount = enriched.Count,
        totalValue = cash.Total,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/orders", async (Trading212Client client, int? limit) =>
{
    var maxItems = Math.Clamp(limit ?? 50, 1, 50);
    var history = await client.GetOrderHistoryAsync(maxItems);

    var items = history.Items.Select(item =>
    {
        var o = item.Order;
        var f = item.Fill;
        var parts = o.Ticker.Split('_');
        var symbol = parts.Length >= 1 ? parts[0] : o.Ticker;
        var dateStr = f?.FilledAt ?? o.CreatedAt;
        var date = dateStr.Length >= 10 ? dateStr[..10] : dateStr;
        var qty = f != null ? Math.Abs(f.Quantity) : Math.Abs(o.FilledQuantity ?? o.Quantity ?? 0);

        return new
        {
            id = o.Id,
            ticker = o.Ticker,
            symbol,
            name = o.Instrument.Name,
            side = o.Side,
            type = o.Type,
            status = o.Status,
            quantity = qty,
            fillPrice = f?.Price,
            netValue = f?.WalletImpact.NetValue,
            realisedPL = f?.WalletImpact.RealisedProfitLoss,
            currency = o.Instrument.Currency,
            date,
            dateCreated = o.CreatedAt
        };
    }).ToList();

    return Results.Json(new
    {
        items,
        count = items.Count
    });
});

app.MapGet("/api/interest", async (Trading212Client client, int? limit) =>
{
    var maxItems = Math.Clamp(limit ?? 50, 1, 50);
    var transactions = await client.GetTransactionsAsync(maxItems);

    // Filter to DEPOSIT type (interest payments are classified as deposits at ~2am daily)
    var interestItems = transactions.Items
        .Where(t => t.Type == "DEPOSIT")
        .Select(t =>
        {
            var date = t.DateTime.Length >= 10 ? t.DateTime[..10] : t.DateTime;
            return new
            {
                amount = t.Amount,
                currency = t.Currency,
                date,
                dateTime = t.DateTime,
                reference = t.Reference
            };
        })
        .ToList();

    var totalInterest = interestItems.Sum(t => t.amount);

    // Monthly breakdown
    var byMonth = interestItems
        .GroupBy(t => t.date.Length >= 7 ? t.date[..7] : t.date)
        .Select(g => new { month = g.Key, total = g.Sum(t => t.amount), count = g.Count() })
        .OrderByDescending(g => g.month)
        .ToList();

    // Daily average
    var dailyAvg = interestItems.Count > 0 ? totalInterest / interestItems.Count : 0;

    // Projected annual (based on daily average)
    var projectedAnnual = dailyAvg * 365;

    return Results.Json(new
    {
        items = interestItems,
        totalInterest,
        dailyAverage = Math.Round(dailyAvg, 2),
        projectedAnnual = Math.Round(projectedAnnual, 2),
        byMonth,
        count = interestItems.Count
    });
});

app.MapGet("/api/position/{ticker}", async (string ticker, Trading212Client client) =>
{
    var summary = await GetCachedSummary(client);
    var totalValue = summary.Cash.Total;

    var position = summary.Positions.FirstOrDefault(p =>
        string.Equals(p.Instrument.Ticker, ticker, StringComparison.OrdinalIgnoreCase));

    if (position is null)
        return Results.NotFound(new { error = "Position not found", ticker });

    var w = position.WalletImpact;
    var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
    var parts = position.Instrument.Ticker.Split('_');
    var exchange = parts.Length >= 3 ? parts[1] :
        position.Instrument.Currency is "GBX" or "GBP" ? "L" :
        position.Instrument.Currency == "EUR" ? "EU" : "?";

    DateTimeOffset? boughtAt = null;
    if (DateTimeOffset.TryParse(position.CreatedAt, out var parsed)) boughtAt = parsed;

    // Fetch orders and dividends for this ticker in parallel
    var ordersTask = client.GetOrderHistoryAsync(50);
    var dividendsTask = client.GetDividendsAsync(50);
    await Task.WhenAll(ordersTask, dividendsTask);

    var orderHistory = await ordersTask;
    var dividendHistory = await dividendsTask;

    // Filter orders for this ticker
    var tickerOrders = orderHistory.Items
        .Where(item => string.Equals(item.Order.Ticker, ticker, StringComparison.OrdinalIgnoreCase))
        .Select(item =>
        {
            var o = item.Order;
            var f = item.Fill;
            var dateStr = f?.FilledAt ?? o.CreatedAt;
            var date = dateStr.Length >= 10 ? dateStr[..10] : dateStr;
            var qty = f != null ? Math.Abs(f.Quantity) : Math.Abs(o.FilledQuantity ?? o.Quantity ?? 0);
            return new
            {
                id = o.Id,
                side = o.Side,
                type = o.Type,
                status = o.Status,
                quantity = qty,
                fillPrice = f?.Price,
                netValue = f?.WalletImpact.NetValue,
                realisedPL = f?.WalletImpact.RealisedProfitLoss,
                date,
                dateCreated = o.CreatedAt
            };
        })
        .OrderByDescending(x => x.date)
        .ToList();

    // Filter dividends for this ticker
    var tickerDividends = dividendHistory.Items
        .Where(d => string.Equals(d.Ticker, ticker, StringComparison.OrdinalIgnoreCase))
        .Select(d => new
        {
            amount = d.Amount,
            quantity = d.Quantity,
            type = d.Type,
            paidOn = d.PaidOn.Length >= 10 ? d.PaidOn[..10] : d.PaidOn
        })
        .OrderByDescending(d => d.paidOn)
        .ToList();

    var totalDividends = tickerDividends.Sum(d => d.amount);

    // Compute total cost basis and realised P&L from orders
    var totalBought = tickerOrders.Where(o => o.side == "BUY").Sum(o => o.netValue != null ? Math.Abs(o.netValue.Value) : 0);
    var totalSold = tickerOrders.Where(o => o.side == "SELL").Sum(o => o.netValue != null ? Math.Abs(o.netValue.Value) : 0);
    var totalRealisedPL = tickerOrders.Sum(o => o.realisedPL ?? 0);

    return Results.Json(new
    {
        ticker = position.Instrument.Ticker,
        name = position.Instrument.Name,
        isin = position.Instrument.Isin,
        currency = position.Instrument.Currency,
        exchange,
        quantity = position.Quantity,
        averagePrice = position.AveragePricePaid,
        currentPrice = position.CurrentPrice,
        investedValue = w.TotalCost,
        currentValue = w.CurrentValue,
        profitLoss = w.UnrealizedProfitLoss,
        profitLossPercent = Math.Round(plPct, 2),
        fxImpact = w.FxImpact,
        weight = totalValue != 0 ? Math.Round(w.CurrentValue / totalValue * 100, 2) : 0,
        boughtAt = boughtAt?.ToString("yyyy-MM-dd"),
        holdingDays = boughtAt.HasValue ? (int)(DateTimeOffset.UtcNow - boughtAt.Value).TotalDays : (int?)null,
        orders = tickerOrders,
        orderCount = tickerOrders.Count,
        totalBought,
        totalSold,
        totalRealisedPL,
        dividends = tickerDividends,
        dividendCount = tickerDividends.Count,
        totalDividends,
        totalReturn = w.UnrealizedProfitLoss + totalRealisedPL + totalDividends,
        timestamp = DateTimeOffset.UtcNow
    });
});

app.MapGet("/api/snapshots", async () =>
{
    if (!File.Exists(snapshotPath))
        return Results.Json(new { snapshots = Array.Empty<object>(), count = 0 });

    var json = await File.ReadAllTextAsync(snapshotPath);
    var snapshots = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];

    return Results.Json(new { snapshots, count = snapshots.Count });
});

app.MapGet("/api/dividend-calendar", async (Trading212Client client) =>
{
    var dividends = await client.GetDividendsAsync(50);
    var summary = await GetCachedSummary(client);
    var heldTickers = summary.Positions.Select(p => p.Instrument.Ticker).ToHashSet();

    // Group dividends by ticker, calculate frequency and project next payment
    var byTicker = dividends.Items
        .GroupBy(d => d.Ticker)
        .Where(g => heldTickers.Contains(g.Key)) // only current holdings
        .Select(g =>
        {
            var payments = g.OrderBy(d => d.PaidOn).ToList();
            var dates = payments
                .Select(d => DateTimeOffset.TryParse(d.PaidOn, out var dt) ? dt : (DateTimeOffset?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            string frequency = "unknown";
            DateTimeOffset? nextExpected = null;
            double avgIntervalDays = 0;

            if (dates.Count >= 2)
            {
                var intervals = dates.Zip(dates.Skip(1), (a, b) => (b - a).TotalDays).ToList();
                avgIntervalDays = intervals.Average();
                frequency = avgIntervalDays switch
                {
                    < 45 => "monthly",
                    < 120 => "quarterly",
                    < 200 => "semi-annual",
                    _ => "annual"
                };
                nextExpected = dates.Last().AddDays(avgIntervalDays);
            }
            else if (dates.Count == 1)
            {
                frequency = "annual";
                nextExpected = dates[0].AddYears(1);
                avgIntervalDays = 365;
            }

            var avgAmount = payments.Average(d => d.Amount);

            return new
            {
                ticker = g.Key,
                symbol = g.Key.Split('_')[0],
                paymentCount = payments.Count,
                lastPayment = dates.LastOrDefault().ToString("yyyy-MM-dd"),
                lastAmount = payments.Last().Amount,
                avgAmount = Math.Round(avgAmount, 2),
                frequency,
                nextExpected = nextExpected?.ToString("yyyy-MM-dd"),
                daysUntilNext = nextExpected.HasValue ? (int)(nextExpected.Value - DateTimeOffset.UtcNow).TotalDays : (int?)null,
                projectedAnnual = Math.Round(avgAmount * (decimal)(365.0 / Math.Max(avgIntervalDays, 1)), 2)
            };
        })
        .OrderBy(x => x.nextExpected)
        .ToList();

    var totalProjectedAnnual = byTicker.Sum(x => x.projectedAnnual);

    return Results.Json(new
    {
        items = byTicker,
        totalProjectedAnnual = Math.Round(totalProjectedAnnual, 2),
        count = byTicker.Count
    });
});

app.MapGet("/api/config", () =>
{
    var config = AlertConfigLoader.Load();
    return Results.Json(new
    {
        environment = envLabel,
        thresholds = new
        {
            market = config.Market,
            portfolio = config.Portfolio
        },
        newsKeywords = config.NewsKeywords.Count,
        newsFeeds = config.NewsFeeds.Count
    });
});

app.MapGet("/api/benchmark", async () =>
{
    // 1. Load portfolio snapshots
    var portfolioPoints = new List<object>();
    var portfolioDates = new HashSet<string>();

    if (File.Exists(snapshotPath))
    {
        var snapshotJson = await File.ReadAllTextAsync(snapshotPath);
        using var snapshotDoc = JsonDocument.Parse(snapshotJson);
        var snapshotArray = snapshotDoc.RootElement;

        if (snapshotArray.GetArrayLength() > 0)
        {
            decimal firstValue = 0;
            foreach (var snap in snapshotArray.EnumerateArray())
            {
                var date = snap.GetProperty("date").GetString() ?? "";
                decimal totalValue = 0;
                if (snap.TryGetProperty("totalValue", out var tv))
                {
                    if (tv.ValueKind == JsonValueKind.Number)
                        totalValue = tv.GetDecimal();
                    else if (decimal.TryParse(tv.GetRawText(), out var parsed))
                        totalValue = parsed;
                }

                if (firstValue == 0) firstValue = totalValue;
                var pctReturn = firstValue != 0
                    ? Math.Round((totalValue - firstValue) / firstValue * 100, 2)
                    : 0m;

                portfolioDates.Add(date);
                portfolioPoints.Add(new { date, value = pctReturn });
            }
        }
    }

    // 2. Fetch benchmark data from Yahoo Finance (full year, not filtered to portfolio dates)
    var benchmarks = new List<object>();

    var benchmarkConfigs = new[]
    {
        new { Name = "S&P 500", Url = "https://query1.finance.yahoo.com/v8/finance/chart/%5EGSPC?interval=1d&range=1y" },
        new { Name = "FTSE 100", Url = "https://query1.finance.yahoo.com/v8/finance/chart/%5EFTSE?interval=1d&range=1y" }
    };

    using var yahooClient = new HttpClient();
    yahooClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; T212Dashboard/1.0)");
    yahooClient.Timeout = TimeSpan.FromSeconds(10);

    foreach (var bench in benchmarkConfigs)
    {
        try
        {
            var response = await yahooClient.GetStringAsync(bench.Url);
            using var doc = JsonDocument.Parse(response);

            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var timestamps = result.GetProperty("timestamp");
            var closes = result
                .GetProperty("indicators")
                .GetProperty("quote")[0]
                .GetProperty("close");

            // Build date→close map with all dates
            var priceByDate = new Dictionary<string, decimal>();
            for (int i = 0; i < timestamps.GetArrayLength(); i++)
            {
                var unixSecs = timestamps[i].GetInt64();
                var date = DateTimeOffset.FromUnixTimeSeconds(unixSecs).UtcDateTime.ToString("yyyy-MM-dd");
                if (closes[i].ValueKind == JsonValueKind.Number)
                {
                    priceByDate[date] = closes[i].GetDecimal();
                }
            }

            // Use ALL benchmark dates, normalize to % returns from first date
            var sortedDates = priceByDate.Keys.OrderBy(d => d).ToList();
            if (sortedDates.Count > 0)
            {
                var firstPrice = priceByDate[sortedDates[0]];
                var dataPoints = sortedDates.Select(d =>
                {
                    var pctReturn = firstPrice != 0
                        ? Math.Round((priceByDate[d] - firstPrice) / firstPrice * 100, 2)
                        : 0m;
                    return new { date = d, value = pctReturn };
                }).ToList();

                benchmarks.Add(new { name = bench.Name, data = dataPoints });
            }
        }
        catch
        {
            // Yahoo Finance unavailable — skip this benchmark
        }
    }

    return Results.Json(new
    {
        portfolio = portfolioPoints,
        benchmarks
    });
});

app.MapFallbackToFile("index.html");

app.Run("http://localhost:5050");

static string StripHtml(string html)
{
    if (string.IsNullOrWhiteSpace(html)) return "";
    return Regex.Replace(html, "<[^>]+>", "").Trim();
}
