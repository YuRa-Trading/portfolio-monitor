using System.Text.Json.Serialization;

namespace Trading212.Shared.Models;

public class AccountCash
{
    [JsonPropertyName("free")]
    public decimal Free { get; set; }

    [JsonPropertyName("invested")]
    public decimal Invested { get; set; }

    [JsonPropertyName("pieCash")]
    public decimal PieValue { get; set; }

    [JsonPropertyName("result")]
    public decimal Result { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("ppl")]
    public decimal Ppl { get; set; }

    [JsonPropertyName("blocked")]
    public decimal Blocked { get; set; }
}

public class AccountInfo
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }
}

public class PositionInstrument
{
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("isin")]
    public string Isin { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
}

public class WalletImpact
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("totalCost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("currentValue")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("unrealizedProfitLoss")]
    public decimal UnrealizedProfitLoss { get; set; }

    [JsonPropertyName("fxImpact")]
    public decimal? FxImpact { get; set; }
}

public class Position
{
    [JsonPropertyName("instrument")]
    public PositionInstrument Instrument { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("currentPrice")]
    public decimal CurrentPrice { get; set; }

    [JsonPropertyName("averagePricePaid")]
    public decimal AveragePricePaid { get; set; }

    [JsonPropertyName("walletImpact")]
    public WalletImpact WalletImpact { get; set; } = new();
}

public class Order
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("limitPrice")]
    public decimal? LimitPrice { get; set; }

    [JsonPropertyName("stopPrice")]
    public decimal? StopPrice { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("filledQuantity")]
    public decimal FilledQuantity { get; set; }

    [JsonPropertyName("creationTime")]
    public string CreationTime { get; set; } = string.Empty;
}

public class DividendResponse
{
    [JsonPropertyName("items")]
    public List<Dividend> Items { get; set; } = [];

    [JsonPropertyName("nextPagePath")]
    public string? NextPagePath { get; set; }
}

public class Dividend
{
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("paidOn")]
    public string PaidOn { get; set; } = string.Empty;
}

public class Instrument
{
    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = string.Empty;

    [JsonPropertyName("isin")]
    public string Isin { get; set; } = string.Empty;
}

public class OrderHistoryResponse
{
    [JsonPropertyName("items")]
    public List<OrderHistoryItem> Items { get; set; } = [];

    [JsonPropertyName("nextPagePath")]
    public string? NextPagePath { get; set; }
}

public class OrderHistoryItem
{
    [JsonPropertyName("order")]
    public HistoricalOrder Order { get; set; } = new();

    [JsonPropertyName("fill")]
    public OrderFill? Fill { get; set; }
}

public class HistoricalOrder
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("strategy")]
    public string Strategy { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("ticker")]
    public string Ticker { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; set; }

    [JsonPropertyName("filledQuantity")]
    public decimal? FilledQuantity { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }

    [JsonPropertyName("filledValue")]
    public decimal? FilledValue { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("instrument")]
    public PositionInstrument Instrument { get; set; } = new();
}

public class OrderFill
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("filledAt")]
    public string FilledAt { get; set; } = string.Empty;

    [JsonPropertyName("walletImpact")]
    public OrderWalletImpact WalletImpact { get; set; } = new();
}

public class OrderWalletImpact
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("netValue")]
    public decimal NetValue { get; set; }

    [JsonPropertyName("realisedProfitLoss")]
    public decimal? RealisedProfitLoss { get; set; }

    [JsonPropertyName("fxRate")]
    public decimal? FxRate { get; set; }
}

public class TransactionResponse
{
    [JsonPropertyName("items")]
    public List<Transaction> Items { get; set; } = [];

    [JsonPropertyName("nextPagePath")]
    public string? NextPagePath { get; set; }
}

public class Transaction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("dateTime")]
    public string DateTime { get; set; } = string.Empty;
}

public class PortfolioSummary
{
    public AccountCash Cash { get; set; } = new();
    public List<Position> Positions { get; set; } = [];
    public Dictionary<string, Instrument> Instruments { get; set; } = [];
}

public class EarningsEventData
{
    public decimal? Actual { get; set; }
    public decimal? Estimate { get; set; }
    public string? Period { get; set; }
    public decimal? Surprise { get; set; }
    public decimal? SurprisePercent { get; set; }
    public string? Symbol { get; set; }
    public int? Year { get; set; }
    public int? Quarter { get; set; }
}

public class CachedEarningsDocument
{
    public string Id { get; set; } = string.Empty;
    public DateTime FetchedAtUtc { get; set; }
    public List<EarningsEventData> Events { get; set; } = [];
}

public class CachedPortfolioDocument
{
    public string Id { get; set; } = "latest";
    public DateTime FetchedAtUtc { get; set; }
    public AccountCash Cash { get; set; } = new();
    public List<Position> Positions { get; set; } = [];
}

public class PortfolioSnapshotDocument
{
    public string Id { get; set; } = string.Empty; // "yyyy-MM-dd"
    public decimal TotalValue { get; set; }
    public decimal Invested { get; set; }
    public decimal Pnl { get; set; }
    public decimal PnlPct { get; set; }
    public decimal FreeCash { get; set; }
    public int PositionCount { get; set; }
}
