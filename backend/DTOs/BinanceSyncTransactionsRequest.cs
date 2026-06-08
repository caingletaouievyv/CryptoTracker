namespace CryptoTracker.DTOs;

/// <summary>Binance Spot myTrades sync. Symbols like BTCUSDT (required per Binance API). Keys optional if Binance:ApiKey/SecretKey set.</summary>
public class BinanceSyncTransactionsRequest
{
    public string? ApiKey { get; set; }
    public string? SecretKey { get; set; }

    /// <summary>Spot symbols to pull (e.g. BTCUSDT). If null/empty, uses Binance:Symbols (comma-separated) from config.</summary>
    public string[]? Symbols { get; set; }

    /// <summary>Days to scan in 24h API windows when the default &quot;recent&quot; query returns no rows. Use 0 or omit with Binance:HistoryLookbackDays 0 to scan up to Binance:HistoryLookbackMaxDays. Otherwise clamped to that max.</summary>
    public int? HistoryLookbackDays { get; set; }
}
