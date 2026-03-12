using System.ComponentModel;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ModelContextProtocol.Server;
using Trading212.Shared;
using Trading212.Shared.Config;

namespace Trading212McpServer;

[McpServerToolType]
public partial class NewsAndAlertTools
{
    private readonly Trading212Client _client;
    private readonly HttpClient _httpClient;

    public NewsAndAlertTools(Trading212Client client)
    {
        _client = client;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (compatible; T212MCP/1.0)");
    }

    // ─────────────────────────────────────────────────────────────
    //  Tool 1: get_portfolio_news
    // ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_portfolio_news")]
    [Description("Fetch and filter recent financial news from RSS feeds, scored by relevance to your portfolio " +
        "keywords (Iran, OPEC, oil, gold, defence, Rolls-Royce, BP, etc). " +
        "Returns items from the last 48 hours sorted by relevance score.")]
    public async Task<string> GetPortfolioNews(
        [Description("Maximum number of news items to return (default 15)")] int limit = 15)
    {
        var config = AlertConfigLoader.Load();
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("                   PORTFOLIO NEWS FEED");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        var cutoff = DateTimeOffset.UtcNow.AddHours(-48);
        var scored = new List<ScoredNewsItem>();

        foreach (var feedUrl in config.NewsFeeds)
        {
            try
            {
                var items = await FetchFeedAsync(feedUrl, cutoff);
                foreach (var item in items)
                {
                    var score = ScoreItem(item.Title, item.Summary, config.NewsKeywords);
                    if (score > 0)
                    {
                        scored.Add(new ScoredNewsItem
                        {
                            Title = item.Title,
                            Summary = item.Summary,
                            Link = item.Link,
                            Published = item.Published,
                            Source = item.Source,
                            Score = score
                        });
                    }
                }
            }
            catch
            {
                // Skip failing feeds silently — other feeds may still work
            }
        }

        if (scored.Count == 0)
        {
            sb.AppendLine("  No relevant news found in the last 48 hours.");
            sb.AppendLine();
            sb.AppendLine($"  Monitored {config.NewsFeeds.Count} feeds with {config.NewsKeywords.Count} keywords.");
            return sb.ToString();
        }

        var results = scored
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Published)
            .Take(limit)
            .ToList();

        sb.AppendLine($"  {results.Count} relevant item(s) from {config.NewsFeeds.Count} feeds");
        sb.AppendLine();

        foreach (var item in results)
        {
            var dots = new string('●', Math.Min(item.Score, 5));
            var age = FormatAge(item.Published);
            var summary = Truncate(item.Summary, 150);

            sb.AppendLine($"  {dots,-5}  {item.Title}");
            sb.AppendLine($"         {age} · {item.Source}");
            if (!string.IsNullOrWhiteSpace(summary))
                sb.AppendLine($"         {summary}");
            sb.AppendLine($"         {item.Link}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────
    //  Tool 2: get_market_alerts
    // ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_market_alerts")]
    [Description("Check portfolio positions against alert thresholds: drawdown exceeding limit, " +
        "over-concentration in a single position, undersized positions, too many positions, " +
        "and excessive cash allocation. Returns prioritised alerts or all-clear status.")]
    public async Task<string> GetMarketAlerts()
    {
        var config = AlertConfigLoader.Load();
        var sb = new StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine("                    PORTFOLIO ALERTS");
        sb.AppendLine("═══════════════════════════════════════════════════════════");
        sb.AppendLine();

        try
        {
            var summary = await _client.GetPortfolioSummaryAsync();
            var cash = summary.Cash;
            var totalValue = cash.Total;
            var alerts = new List<(string Severity, string Emoji, string Message)>();

            var enriched = summary.Positions.Select(p =>
            {
                var w = p.WalletImpact;
                var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
                return new
                {
                    Ticker = p.Instrument.Ticker,
                    Name = p.Instrument.Name,
                    CurrentValue = w.CurrentValue,
                    PlPct = plPct
                };
            }).ToList();

            // Check each position
            foreach (var pos in enriched)
            {
                // Drawdown check
                if (pos.PlPct < -config.Portfolio.MaxDrawdownPercent)
                {
                    alerts.Add(("HIGH", "\ud83d\udd34",
                        $"{pos.Ticker} ({pos.Name}) — Down {Math.Abs(pos.PlPct):N1}% — exceeds {config.Portfolio.MaxDrawdownPercent}% threshold"));
                }

                // Concentration check
                if (totalValue != 0)
                {
                    var concentration = pos.CurrentValue / totalValue * 100;
                    if (concentration > config.Portfolio.MaxConcentrationPercent)
                    {
                        alerts.Add(("WARN", "\ud83d\udfe1",
                            $"{pos.Ticker} ({pos.Name}) — {concentration:N1}% of portfolio — exceeds {config.Portfolio.MaxConcentrationPercent}% limit"));
                    }
                }

                // Position size check
                if (pos.CurrentValue < config.Portfolio.MinPositionSize)
                {
                    alerts.Add(("SIZE", "\ud83d\udfe0",
                        $"{pos.Ticker} ({pos.Name}) — \u00a3{pos.CurrentValue:N2} — below \u00a3{config.Portfolio.MinPositionSize:N0} minimum"));
                }
            }

            // Total position count check
            if (enriched.Count > config.Portfolio.MaxPositions)
            {
                alerts.Add(("WARN", "\ud83d\udfe1",
                    $"Position count: {enriched.Count} — exceeds {config.Portfolio.MaxPositions} maximum"));
            }

            // Cash percentage check
            if (totalValue != 0)
            {
                var cashPct = cash.Free / totalValue * 100;
                if (cashPct > 30)
                {
                    alerts.Add(("WARN", "\ud83d\udfe1",
                        $"Cash allocation: {cashPct:N1}% (\u00a3{cash.Free:N2}) — exceeds 30% threshold"));
                }
            }

            // Output
            if (alerts.Count == 0)
            {
                sb.AppendLine("  \u2705 NO ALERTS — all positions within thresholds");
                sb.AppendLine();
                sb.AppendLine("  Current thresholds:");
                sb.AppendLine($"    Max drawdown:       {config.Portfolio.MaxDrawdownPercent}%");
                sb.AppendLine($"    Max concentration:  {config.Portfolio.MaxConcentrationPercent}%");
                sb.AppendLine($"    Min position size:  \u00a3{config.Portfolio.MinPositionSize:N0}");
                sb.AppendLine($"    Max positions:      {config.Portfolio.MaxPositions}");
            }
            else
            {
                sb.AppendLine($"  \u26a0\ufe0f {alerts.Count} ALERT(S) DETECTED");
                sb.AppendLine();

                foreach (var (severity, emoji, message) in alerts)
                {
                    sb.AppendLine($"  {emoji} [{severity}] {message}");
                }
            }

            // Quick stats
            sb.AppendLine();
            sb.AppendLine("  ─── Quick Stats ───────────────────────────────────────");
            sb.AppendLine($"  Account Value:   \u00a3{totalValue:N2}");
            sb.AppendLine($"  Positions:       {enriched.Count}");
            var overallPl = cash.Result;
            var overallPlPct = cash.Invested != 0 ? overallPl / cash.Invested * 100 : 0;
            sb.AppendLine($"  Overall P&L:     \u00a3{overallPl:N2} ({overallPlPct:N2}%)");

            return sb.ToString();
        }
        catch (HttpRequestException ex)
        {
            sb.AppendLine($"  Error fetching portfolio data: {ex.Message}");
            return sb.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Tool 3: get_daily_briefing
    // ─────────────────────────────────────────────────────────────

    [McpServerTool(Name = "get_daily_briefing")]
    [Description("Complete daily briefing combining portfolio snapshot, alert checks, and relevant news. " +
        "Shows account value, P&L, best/worst performers, active alerts, and top news stories.")]
    public async Task<string> GetDailyBriefing()
    {
        var sb = new StringBuilder();
        var now = DateTime.Now;

        // Header
        sb.AppendLine("╔═══════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           YuRa Trading — Daily Briefing                  ║");
        sb.AppendLine($"║           {now:dddd, dd MMMM yyyy · HH:mm}                 ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════╝");
        sb.AppendLine();

        // Section 1: Portfolio Snapshot
        sb.AppendLine("┌─── PORTFOLIO SNAPSHOT ────────────────────────────────────┐");
        sb.AppendLine();

        try
        {
            var summary = await _client.GetPortfolioSummaryAsync();
            var cash = summary.Cash;
            var totalValue = cash.Total;
            var overallPl = cash.Result;
            var overallPlPct = cash.Invested != 0 ? overallPl / cash.Invested * 100 : 0;
            var cashPct = totalValue != 0 ? cash.Free / totalValue * 100 : 0;

            sb.AppendLine($"  Total Value:     \u00a3{totalValue:N2}");
            sb.AppendLine($"  Total Invested:  \u00a3{cash.Invested:N2}");
            sb.AppendLine($"  Overall P&L:     \u00a3{overallPl:N2} ({overallPlPct:N2}%)");
            sb.AppendLine($"  Free Cash:       \u00a3{cash.Free:N2} ({cashPct:N1}%)");
            sb.AppendLine($"  Positions:       {summary.Positions.Count}");

            if (summary.Positions.Count > 0)
            {
                var enriched = summary.Positions.Select(p =>
                {
                    var w = p.WalletImpact;
                    var plPct = w.TotalCost != 0 ? w.UnrealizedProfitLoss / w.TotalCost * 100 : 0;
                    return new
                    {
                        Ticker = p.Instrument.Ticker,
                        Name = p.Instrument.Name,
                        PlPct = plPct,
                        Pl = w.UnrealizedProfitLoss
                    };
                }).ToList();

                var best = enriched.MaxBy(x => x.PlPct);
                var worst = enriched.MinBy(x => x.PlPct);

                sb.AppendLine();
                if (best is not null)
                    sb.AppendLine($"  \u25b2 Best:   {best.Ticker} ({best.Name}) +{best.PlPct:N1}% (\u00a3{best.Pl:N2})");
                if (worst is not null)
                    sb.AppendLine($"  \u25bc Worst:  {worst.Ticker} ({worst.Name}) {worst.PlPct:N1}% (\u00a3{worst.Pl:N2})");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error loading portfolio: {ex.Message}");
        }

        sb.AppendLine();

        // Section 2: Alerts
        sb.AppendLine("├─── ALERTS ────────────────────────────────────────────────┤");
        sb.AppendLine();

        try
        {
            var alertsOutput = await GetMarketAlerts();
            // Strip the header from GetMarketAlerts since we have our own section header
            var lines = alertsOutput.Split('\n');
            var pastHeader = false;
            foreach (var line in lines)
            {
                if (!pastHeader)
                {
                    if (line.TrimStart().StartsWith('\u2705') || line.TrimStart().StartsWith('\u26a0'))
                        pastHeader = true;
                    else
                        continue;
                }
                if (pastHeader)
                    sb.AppendLine(line.TrimEnd());
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error loading alerts: {ex.Message}");
        }

        sb.AppendLine();

        // Section 3: News
        sb.AppendLine("├─── RELEVANT NEWS ─────────────────────────────────────────┤");
        sb.AppendLine();

        try
        {
            var newsOutput = await GetPortfolioNews(limit: 8);
            // Strip the header from GetPortfolioNews
            var lines = newsOutput.Split('\n');
            var pastHeader = false;
            foreach (var line in lines)
            {
                if (!pastHeader)
                {
                    // The first content line after headers starts with item count or "No relevant"
                    if (line.TrimStart().StartsWith("No relevant") ||
                        (line.Trim().Length > 0 && char.IsDigit(line.Trim()[0])))
                        pastHeader = true;
                    else
                        continue;
                }
                if (pastHeader)
                    sb.AppendLine(line.TrimEnd());
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"  Error loading news: {ex.Message}");
        }

        sb.AppendLine();

        // Footer
        var nextReview = now.Hour < 16
            ? now.Date.AddHours(16)
            : now.Date.AddDays(1).AddHours(8);

        sb.AppendLine("└───────────────────────────────────────────────────────────┘");
        sb.AppendLine($"  Next review: {nextReview:dddd dd MMM · HH:mm}");
        sb.AppendLine("  YuRa Trading — Stay sharp, trade smart.");

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────

    private async Task<List<RssItem>> FetchFeedAsync(string feedUrl, DateTimeOffset cutoff)
    {
        var items = new List<RssItem>();

        using var stream = await _httpClient.GetStreamAsync(feedUrl);
        using var reader = XmlReader.Create(stream);
        var feed = SyndicationFeed.Load(reader);

        var sourceName = feed.Title?.Text ?? new Uri(feedUrl).Host;

        foreach (var entry in feed.Items)
        {
            var published = entry.PublishDate != DateTimeOffset.MinValue
                ? entry.PublishDate
                : entry.LastUpdatedTime;

            if (published < cutoff)
                continue;

            var title = entry.Title?.Text ?? "(no title)";
            var summary = StripHtml(entry.Summary?.Text ?? "");
            var link = entry.Links.FirstOrDefault()?.Uri?.AbsoluteUri ?? "";

            items.Add(new RssItem
            {
                Title = title,
                Summary = summary,
                Link = link,
                Published = published,
                Source = sourceName
            });
        }

        return items;
    }

    private static int ScoreItem(string title, string summary, List<string> keywords)
    {
        var text = $"{title} {summary}";
        var score = 0;

        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score++;
        }

        return score;
    }

    private static string FormatAge(DateTimeOffset published)
    {
        var age = DateTimeOffset.UtcNow - published;

        if (age.TotalMinutes < 60)
            return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalHours < 24)
            return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        text = text.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        return HtmlTagRegex().Replace(html, "").Trim();
    }

    private class RssItem
    {
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Link { get; set; } = "";
        public DateTimeOffset Published { get; set; }
        public string Source { get; set; } = "";
    }

    private class ScoredNewsItem : RssItem
    {
        public int Score { get; set; }
    }
}
