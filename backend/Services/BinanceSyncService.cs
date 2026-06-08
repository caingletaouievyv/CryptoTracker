using System.Globalization;
using CryptoTracker.DTOs;
using CryptoTracker.Exceptions;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CryptoTracker.Services;

public class BinanceSyncService : IBinanceSyncService
{
    private readonly IBinanceService _binanceService;
    private readonly ITransactionService _transactionService;
    private readonly IConfiguration _config;

    public BinanceSyncService(
        IBinanceService binanceService,
        ITransactionService transactionService,
        IConfiguration config)
    {
        _binanceService = binanceService;
        _transactionService = transactionService;
        _config = config;
    }

    public async Task<OkxSyncResultDto> SyncBinanceSpotTradesAsync(
        BinanceSyncTransactionsRequest? body,
        CancellationToken cancellationToken = default)
    {
        var creds = ResolveCredentials(body);
        if (creds == null)
        {
            throw new ArgumentException(
                "Provide Binance apiKey and secretKey in the request body, or set Binance:ApiKey and Binance:SecretKey in configuration.");
        }

        var symbols = ResolveSymbols(body);
        if (symbols.Count == 0)
        {
            throw new ArgumentException(
                "Provide symbols in the body (e.g. [\"BTCUSDT\",\"ETHUSDT\"]) or set Binance:Symbols in appsettings (comma-separated).");
        }

        IReadOnlyDictionary<string, (string BaseAsset, string QuoteAsset)> map;
        try
        {
            map = await _binanceService.GetTradingSymbolMapAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new AppHttpException(StatusCodes.Status502BadGateway,
                "Cannot reach Binance. Check your network or firewall.");
        }

        var unknown = symbols.Where(s => !map.ContainsKey(s)).ToList();
        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                "Unknown or non-TRADING Binance symbols: " + string.Join(", ", unknown));
        }

        var maxDays = Math.Clamp(_config.GetValue("Binance:HistoryLookbackMaxDays", 3650), 1, 3650);
        var requested = body?.HistoryLookbackDays ?? _config.GetValue("Binance:HistoryLookbackDays", 0);
        var lookbackDays = requested <= 0 ? maxDays : Math.Clamp(requested, 1, maxDays);

        var requests = new List<CreateTransactionRequest>();
        foreach (var sym in symbols)
        {
            var (baseAsset, quoteAsset) = map[sym];
            IReadOnlyList<BinanceMyTradeRow> trades;
            try
            {
                trades = await _binanceService.GetAllMyTradesAsync(sym, creds, lookbackDays, cancellationToken);
            }
            catch (HttpRequestException)
            {
                throw new AppHttpException(StatusCodes.Status502BadGateway,
                    "Cannot reach Binance. Check your network or firewall.");
            }

            foreach (var t in trades)
            {
                var row = MapTrade(t, baseAsset, quoteAsset);
                if (row != null)
                    requests.Add(row);
            }
        }

        if (requests.Count == 0)
        {
            return new OkxSyncResultDto
            {
                Synced = 0,
                Updated = 0,
                Message =
                    $"No Spot myTrades in the last {lookbackDays} days (max {maxDays} via Binance:HistoryLookbackMaxDays) for these symbols on this account/API. If you only trade on another exchange (e.g. OKX), Binance will stay empty. Otherwise check: (1) Binance.com vs Binance.US — set Binance:BaseUrl (e.g. https://api.binance.us). (2) Spot only — not Futures/Convert. (3) API key for the same account/sub-account that traded Spot on Binance. (4) IP allowlist."
            };
        }

        var synced = await _transactionService.AddTransactionsAsync(requests, cancellationToken);
        var updated = await _transactionService.BackfillPricesAsync(cancellationToken);
        return new OkxSyncResultDto
        {
            Synced = synced,
            Updated = updated,
            Message = "Transactions synced from Binance Spot; prices backfilled where missing."
        };
    }

    private static CreateTransactionRequest? MapTrade(BinanceMyTradeRow t, string baseAsset, string quoteAsset)
    {
        if (!decimal.TryParse(t.Price, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            return null;
        if (!decimal.TryParse(t.Qty, NumberStyles.Any, CultureInfo.InvariantCulture, out var qty))
            return null;
        if (!decimal.TryParse(t.Commission, NumberStyles.Any, CultureInfo.InvariantCulture, out var commission))
            commission = 0m;

        var feeInQuote = string.Equals(t.CommissionAsset, quoteAsset, StringComparison.OrdinalIgnoreCase)
            ? commission
            : 0m;

        return new CreateTransactionRequest
        {
            Symbol = baseAsset,
            Type = t.IsBuyer ? "Buy" : "Sell",
            Quantity = qty,
            PriceAtTransaction = price,
            Fee = feeInQuote,
            Date = DateTimeOffset.FromUnixTimeMilliseconds(t.Time).UtcDateTime,
            BaseCurrency = quoteAsset,
            Notes = "Binance:" + t.Id
        };
    }

    private BinanceCredentials? ResolveCredentials(BinanceSyncTransactionsRequest? body)
    {
        var k = body?.ApiKey?.Trim() ?? "";
        var s = body?.SecretKey?.Trim() ?? "";
        if (k.Length > 0 && s.Length > 0)
            return new BinanceCredentials { ApiKey = k, SecretKey = s };
        var ck = _config["Binance:ApiKey"]?.Trim() ?? "";
        var cs = _config["Binance:SecretKey"]?.Trim() ?? "";
        if (ck.Length > 0 && cs.Length > 0)
            return new BinanceCredentials { ApiKey = ck, SecretKey = cs };
        return null;
    }

    private List<string> ResolveSymbols(BinanceSyncTransactionsRequest? body)
    {
        if (body?.Symbols is { Length: > 0 })
        {
            return body.Symbols
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct()
                .ToList();
        }

        var cfg = _config["Binance:Symbols"] ?? "";
        return cfg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToUpperInvariant())
            .Where(x => x.Length > 0)
            .Distinct()
            .ToList();
    }
}
