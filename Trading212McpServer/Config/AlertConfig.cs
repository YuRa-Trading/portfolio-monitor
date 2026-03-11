using Microsoft.Extensions.Configuration;

namespace Trading212McpServer.Config;

public class AlertConfig
{
    public MarketThresholds Market { get; set; } = new();
    public PortfolioThresholds Portfolio { get; set; } = new();
    public List<string> NewsKeywords { get; set; } = [];
    public List<string> NewsFeeds { get; set; } = [];
}

public class MarketThresholds
{
    public decimal OilFloor { get; set; }
    public decimal OilCeiling { get; set; }
    public decimal GoldFloor { get; set; }
    public decimal GoldCeiling { get; set; }
    public decimal VixCeiling { get; set; }
}

public class PortfolioThresholds
{
    public decimal MaxDrawdownPercent { get; set; }
    public decimal MaxConcentrationPercent { get; set; }
    public decimal MinPositionSize { get; set; }
    public int MaxPositions { get; set; }
}

public static class AlertConfigLoader
{
    private static AlertConfig? _cached;

    public static AlertConfig Load(IConfiguration? configuration = null)
    {
        if (_cached is not null) return _cached;

        if (configuration is not null)
        {
            _cached = new AlertConfig();
            configuration.GetSection("Alerts").Bind(_cached);
            return _cached;
        }

        // Fallback: return empty config (appsettings.json defaults will apply)
        _cached = new AlertConfig();
        return _cached;
    }
}
