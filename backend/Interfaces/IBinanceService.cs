using CryptoTracker.DTOs;

namespace CryptoTracker.Interfaces;

public interface IBinanceService
{
    /// <summary>TRADING symbols → (baseAsset, quoteAsset) from public exchangeInfo.</summary>
    Task<IReadOnlyDictionary<string, (string BaseAsset, string QuoteAsset)>> GetTradingSymbolMapAsync(CancellationToken cancellationToken = default);

    /// <summary>Spot myTrades for one symbol. Uses recent+endTime pagination; if empty, scans up to historyLookbackDays in 24h windows (capped by Binance:HistoryLookbackMaxDays).</summary>
    Task<IReadOnlyList<BinanceMyTradeRow>> GetAllMyTradesAsync(
        string symbol,
        BinanceCredentials credentials,
        int historyLookbackDays,
        CancellationToken cancellationToken = default);
}
