using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using CryptoTracker.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CryptoTracker.Services;

public class PriceService : IPriceService
{
    private const string HttpClientName = "PriceProvider";
    private static readonly TimeSpan SpotCacheTtl = TimeSpan.FromMinutes(10);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private static readonly ConcurrentDictionary<string, string> SymbolToId = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Stablecoins = new(StringComparer.OrdinalIgnoreCase) { "USDT", "USDC", "DAI", "BUSD", "TUSD", "USDP", "FRAX" };
    private static readonly SemaphoreSlim ListLock = new(1, 1);

    private static readonly Dictionary<string, string> WellKnownGeckoId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "bitcoin",
        ["ETH"] = "ethereum",
        ["XRP"] = "ripple",
        ["SOL"] = "solana",
        ["AVAX"] = "avalanche-2",
        ["LINK"] = "chainlink",
        ["DOGE"] = "dogecoin",
        ["ADA"] = "cardano",
        ["DOT"] = "polkadot",
        ["MATIC"] = "matic-network",
        ["POL"] = "matic-network",
        ["BNB"] = "binancecoin",
        ["ATOM"] = "cosmos",
        ["LTC"] = "litecoin",
        ["BCH"] = "bitcoin-cash",
        ["XLM"] = "stellar",
        ["NEAR"] = "near",
        ["APT"] = "aptos",
        ["ARB"] = "arbitrum",
        ["OP"] = "optimism",
        ["WBTC"] = "wrapped-bitcoin",
        ["SHIB"] = "shiba-inu",
        ["TRX"] = "tron",
        ["TON"] = "the-open-network",
        ["UNI"] = "uniswap",
        ["PI"] = "pi-network",
        ["NIGHT"] = "midnight",
    };

    public PriceService(IHttpClientFactory httpClientFactory, IConfiguration config, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<string, decimal?>> GetSpotPricesUsdAsync(
        IReadOnlyList<string> symbols,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        var distinct = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var s in distinct)
            result[s] = null;

        foreach (var sym in distinct.Where(Stablecoins.Contains))
            result[sym] = 1m;

        var need = distinct.Where(s => !Stablecoins.Contains(s)).ToList();
        if (need.Count == 0)
            return result;

        foreach (var sym in need)
        {
            if (TryGetCachedSpot(sym, out var cached))
                result[sym] = cached;
        }

        var missing = need.Where(s => !result[s].HasValue).ToList();
        if (missing.Count == 0)
            return result;

        var baseUrl = _config["PriceProvider:CoinGeckoBaseUrl"] ?? "https://api.coingecko.com/api/v3";
        var isTodayUtc = asOfDate.Date == DateTime.UtcNow.Date;

        if (isTodayUtc)
        {
            await FetchAndMergeTodayPricesAsync(missing, result, baseUrl, cancellationToken);
            return result;
        }

        foreach (var sym in missing)
            result[sym] = await GetPriceInUsdAsync(sym, asOfDate, cancellationToken);

        return result;
    }

    public async Task<decimal?> GetPriceInUsdAsync(string symbol, DateTime date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;
        symbol = symbol.Trim();
        if (Stablecoins.Contains(symbol))
            return 1m;

        if (date.Date == DateTime.UtcNow.Date && TryGetCachedSpot(symbol, out var cached))
            return cached;

        var baseUrl = _config["PriceProvider:CoinGeckoBaseUrl"] ?? "https://api.coingecko.com/api/v3";

        if (date.Date == DateTime.UtcNow.Date)
        {
            var id = await ResolveCoinGeckoIdAsync(symbol, baseUrl, cancellationToken);
            if (id != null)
            {
                var prices = await FetchCoinGeckoSimplePricesAsync([id], baseUrl, cancellationToken);
                if (prices.TryGetValue(id, out var spot))
                {
                    CacheSpotPrice(symbol, spot);
                    return spot;
                }
            }
        }
        else
        {
            var historical = await TryCoinGeckoHistoryAsync(symbol, date, baseUrl, cancellationToken);
            if (historical.HasValue)
                return historical;
        }

        return await TryCryptoCompareAsync(symbol, date, cancellationToken);
    }

    private async Task FetchAndMergeTodayPricesAsync(
        IReadOnlyList<string> symbols,
        Dictionary<string, decimal?> result,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var symToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sym in symbols)
        {
            var id = await ResolveCoinGeckoIdAsync(sym, baseUrl, cancellationToken);
            if (id != null)
                symToId[sym] = id;
        }

        if (symToId.Count == 0)
            return;

        var idToPrice = await FetchCoinGeckoSimplePricesAsync(symToId.Values.Distinct(StringComparer.OrdinalIgnoreCase), baseUrl, cancellationToken);
        foreach (var (sym, id) in symToId)
        {
            if (!idToPrice.TryGetValue(id, out var price))
                continue;
            result[sym] = price;
            CacheSpotPrice(sym, price);
        }
    }

    private async Task<Dictionary<string, decimal>> FetchCoinGeckoSimplePricesAsync(
        IEnumerable<string> geckoIds,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var ids = geckoIds.ToList();
        if (ids.Count == 0)
            return prices;

        const int chunkSize = 40;
        for (var i = 0; i < ids.Count; i += chunkSize)
        {
            var chunk = ids.Skip(i).Take(chunkSize).ToList();
            var idsParam = string.Join(',', chunk.Select(Uri.EscapeDataString));
            var url = $"{baseUrl}/simple/price?ids={idsParam}&vs_currencies=usd";

            var parsed = await TryFetchCoinGeckoSimpleChunkAsync(url, cancellationToken);
            if (parsed == null && i == 0)
            {
                await Task.Delay(1500, cancellationToken);
                parsed = await TryFetchCoinGeckoSimpleChunkAsync(url, cancellationToken);
            }

            if (parsed == null)
                continue;

            foreach (var (id, price) in parsed)
                prices[id] = price;
        }

        return prices;
    }

    private async Task<Dictionary<string, decimal>?> TryFetchCoinGeckoSimpleChunkAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient();
            var res = await client.GetAsync(url, cancellationToken);
            if (res.StatusCode == HttpStatusCode.TooManyRequests)
                return null;
            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var parsed = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Value.TryGetProperty("usd", out var usdEl))
                    continue;
                if (TryReadUsd(usdEl, out var price))
                    parsed[prop.Name] = price;
            }
            return parsed.Count > 0 ? parsed : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<decimal?> TryCoinGeckoHistoryAsync(string symbol, DateTime date, string baseUrl, CancellationToken ct)
    {
        var id = await ResolveCoinGeckoIdAsync(symbol, baseUrl, ct);
        if (id == null)
            return null;

        var dateStr = date.ToString("dd-MM-yyyy");
        try
        {
            var res = await CreateClient().GetAsync($"{baseUrl}/coins/{Uri.EscapeDataString(id)}/history?date={dateStr}", ct);
            if (!res.IsSuccessStatusCode)
                return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("market_data", out var md) &&
                md.TryGetProperty("current_price", out var cp) &&
                cp.TryGetProperty("usd", out var usd) &&
                TryReadUsd(usd, out var price))
                return price;
        }
        catch
        {
            // fall through
        }
        return null;
    }

    private async Task<string?> ResolveCoinGeckoIdAsync(string symbol, string baseUrl, CancellationToken ct)
    {
        if (WellKnownGeckoId.TryGetValue(symbol, out var known))
            return known;

        var key = symbol.ToLowerInvariant();
        if (SymbolToId.TryGetValue(key, out var cached))
            return cached;

        await EnsureCoinGeckoListFetchedAsync(baseUrl, ct);
        return SymbolToId.TryGetValue(key, out var id) ? id : null;
    }

    private async Task EnsureCoinGeckoListFetchedAsync(string baseUrl, CancellationToken ct)
    {
        if (SymbolToId.Count > 0)
            return;
        await ListLock.WaitAsync(ct);
        try
        {
            if (SymbolToId.Count > 0)
                return;
            var res = await CreateClient().GetAsync($"{baseUrl}/coins/list", ct);
            if (!res.IsSuccessStatusCode)
                return;
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.TryGetProperty("symbol", out var s) && el.TryGetProperty("id", out var id))
                {
                    var sym = s.GetString()?.ToLowerInvariant();
                    var idStr = id.GetString();
                    if (!string.IsNullOrEmpty(sym) && !string.IsNullOrEmpty(idStr))
                        SymbolToId.TryAdd(sym, idStr);
                }
            }
        }
        catch
        {
            // Next request can retry
        }
        finally
        {
            ListLock.Release();
        }
    }

    private async Task<decimal?> TryCryptoCompareAsync(string symbol, DateTime date, CancellationToken ct)
    {
        var apiKey = _config["PriceProvider:CryptoCompareApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        var baseUrl = _config["PriceProvider:CryptoCompareBaseUrl"] ?? "https://min-api.cryptocompare.com";
        var ts = new DateTimeOffset(date.Date).ToUnixTimeSeconds();
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Apikey {apiKey}");

        try
        {
            var url = $"{baseUrl}/data/pricehistorical?fsym={Uri.EscapeDataString(symbol.ToUpperInvariant())}&tsyms=USD&ts={ts}";
            var res = await client.GetAsync(url, ct);
            if (!res.IsSuccessStatusCode)
                return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(symbol.ToUpperInvariant(), out var symEl) &&
                symEl.TryGetProperty("USD", out var usd) &&
                TryReadUsd(usd, out var price))
                return price;
        }
        catch
        {
            // Ignore
        }
        return null;
    }

    private static bool TryReadUsd(JsonElement usdEl, out decimal price)
    {
        price = 0;
        if (usdEl.TryGetDecimal(out price) && price > 0)
            return true;
        if (usdEl.TryGetDouble(out var d) && d > 0)
        {
            price = (decimal)d;
            return true;
        }
        return false;
    }

    private void CacheSpotPrice(string symbol, decimal price) =>
        _cache.Set(CacheKey(symbol), price, SpotCacheTtl);

    private bool TryGetCachedSpot(string symbol, out decimal price) =>
        _cache.TryGetValue(CacheKey(symbol), out price);

    private static string CacheKey(string symbol) => $"spot:{symbol.ToUpperInvariant()}";

    private HttpClient CreateClient() => _httpClientFactory.CreateClient(HttpClientName);
}
