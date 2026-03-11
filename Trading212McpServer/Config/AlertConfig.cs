using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trading212McpServer.Config;

public class AlertConfig
{
    [JsonPropertyName("Market")]
    public MarketThresholds Market { get; set; } = new();

    [JsonPropertyName("Portfolio")]
    public PortfolioThresholds Portfolio { get; set; } = new();

    [JsonPropertyName("NewsKeywords")]
    public List<string> NewsKeywords { get; set; } = [];

    [JsonPropertyName("NewsFeeds")]
    public List<string> NewsFeeds { get; set; } = [];
}

public class MarketThresholds
{
    [JsonPropertyName("OilFloor")]
    public decimal OilFloor { get; set; }

    [JsonPropertyName("OilCeiling")]
    public decimal OilCeiling { get; set; }

    [JsonPropertyName("GoldFloor")]
    public decimal GoldFloor { get; set; }

    [JsonPropertyName("GoldCeiling")]
    public decimal GoldCeiling { get; set; }

    [JsonPropertyName("VixCeiling")]
    public decimal VixCeiling { get; set; }
}

public class PortfolioThresholds
{
    [JsonPropertyName("MaxDrawdownPercent")]
    public decimal MaxDrawdownPercent { get; set; }

    [JsonPropertyName("MaxConcentrationPercent")]
    public decimal MaxConcentrationPercent { get; set; }

    [JsonPropertyName("MinPositionSize")]
    public decimal MinPositionSize { get; set; }

    [JsonPropertyName("MaxPositions")]
    public int MaxPositions { get; set; }
}

public static class AlertConfigLoader
{
    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "alerts.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly object _lock = new();
    private static AlertConfig? _cached;

    public static AlertConfig Load()
    {
        if (_cached is not null) return _cached;

        lock (_lock)
        {
            if (_cached is not null) return _cached;

            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                _cached = JsonSerializer.Deserialize<AlertConfig>(json, JsonOptions)
                    ?? CreateDefault();
                return _cached;
            }

            _cached = CreateDefault();
            var defaultJson = JsonSerializer.Serialize(_cached, JsonOptions);
            File.WriteAllText(ConfigPath, defaultJson);
            return _cached;
        }
    }

    private static AlertConfig CreateDefault() => new()
    {
        Market = new MarketThresholds
        {
            OilFloor = 60,
            OilCeiling = 120,
            GoldFloor = 1800,
            GoldCeiling = 3500,
            VixCeiling = 30
        },
        Portfolio = new PortfolioThresholds
        {
            MaxDrawdownPercent = 20,
            MaxConcentrationPercent = 30,
            MinPositionSize = 500,
            MaxPositions = 30
        },
        NewsKeywords =
        [
            "stock market", "interest rate", "inflation", "earnings",
            "OPEC", "oil price", "gold price", "Federal Reserve",
            "recession", "GDP"
        ],
        NewsFeeds =
        [
            "https://feeds.bbci.co.uk/news/business/rss.xml",
            "https://feeds.bbci.co.uk/news/world/rss.xml",
            "https://www.cnbc.com/id/100003114/device/rss/rss.html",
            "https://feeds.reuters.com/reuters/businessNews",
            "https://feeds.reuters.com/reuters/topNews"
        ]
    };
}
