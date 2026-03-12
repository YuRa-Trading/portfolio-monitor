using LiteDB;
using Trading212.Shared.Models;

namespace Trading212.Shared.Services;

public class CacheService : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly long _accountId;
    private readonly ILiteCollection<CachedEarningsDocument> _earnings;
    private readonly ILiteCollection<CachedPortfolioDocument> _portfolio;
    private readonly ILiteCollection<PortfolioSnapshotDocument> _snapshots;
    private static readonly TimeSpan EarningsTtl = TimeSpan.FromHours(12);

    public CacheService(long accountId, string? dbPath = null)
    {
        _accountId = accountId;
        dbPath ??= DefaultDbPath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new LiteDatabase($"Filename={dbPath};Connection=shared");
        _earnings = _db.GetCollection<CachedEarningsDocument>("earnings");
        _portfolio = _db.GetCollection<CachedPortfolioDocument>("portfolio");
        _snapshots = _db.GetCollection<PortfolioSnapshotDocument>("snapshots");
    }

    public static string DefaultDbPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Trading212MCP",
        "cache.db");

    // ─── Earnings ────────────────────────────────────────────────

    public List<EarningsEventData>? GetEarningsIfFresh(string symbol)
    {
        var doc = _earnings.FindById(symbol);
        if (doc is null) return null;
        if (DateTime.UtcNow - doc.FetchedAtUtc > EarningsTtl) return null;
        return doc.Events;
    }

    public void UpsertEarnings(string symbol, List<EarningsEventData> events)
    {
        _earnings.Upsert(new CachedEarningsDocument
        {
            Id = symbol,
            FetchedAtUtc = DateTime.UtcNow,
            Events = events
        });
    }

    // ─── Portfolio ───────────────────────────────────────────────

    public CachedPortfolioDocument? GetPortfolio()
    {
        return _portfolio.FindById($"{_accountId}");
    }

    public void SavePortfolio(PortfolioSummary summary)
    {
        _portfolio.Upsert(new CachedPortfolioDocument
        {
            Id = $"{_accountId}",
            FetchedAtUtc = DateTime.UtcNow,
            Cash = summary.Cash,
            Positions = summary.Positions
        });

        // Auto-save daily snapshot
        SaveSnapshotIfNeeded(summary);
    }

    // ─── Snapshots ───────────────────────────────────────────────

    public List<PortfolioSnapshotDocument> GetSnapshots()
    {
        var prefix = $"{_accountId}:";
        return _snapshots.Find(s => s.Id.StartsWith(prefix))
            .OrderBy(s => s.Id)
            .ToList();
    }

    private void SaveSnapshotIfNeeded(PortfolioSummary summary)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var snapshotId = $"{_accountId}:{today}";
        if (_snapshots.FindById(snapshotId) is not null) return;

        var invested = summary.Cash.Invested;
        var pnlPct = invested != 0 ? Math.Round(summary.Cash.Result / invested * 100, 2) : 0;

        _snapshots.Upsert(new PortfolioSnapshotDocument
        {
            Id = snapshotId,
            TotalValue = summary.Cash.Total,
            Invested = invested,
            Pnl = summary.Cash.Result,
            PnlPct = pnlPct,
            FreeCash = summary.Cash.Free,
            PositionCount = summary.Positions.Count
        });

        // Keep max 365 days per account
        var all = GetSnapshots();
        if (all.Count > 365)
        {
            foreach (var old in all.Take(all.Count - 365))
                _snapshots.Delete(old.Id);
        }
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
