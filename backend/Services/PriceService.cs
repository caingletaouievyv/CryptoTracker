using System.Collections.Concurrent;
using System.Text.Json;

using CryptoTracker.Interfaces;

namespace CryptoTracker.Services;

public class PriceService : IPriceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private static readonly ConcurrentDictionary<string, string> SymbolToId = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Stablecoins = new(StringComparer.OrdinalIgnoreCase) { "USDT", "USDC", "DAI", "BUSD", "TUSD", "USDP", "FRAX" };
    private static readonly SemaphoreSlim ListLock = new(1, 1);

    /// <summary>Major tickers → CoinGecko id. Avoids wrong /coins/list match when many tokens share a symbol.</summary>
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
    };

    public PriceService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    /// <inheritdoc />
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

        foreach (var sym in distinct)
        {
            if (Stablecoins.Contains(sym))
                result[sym] = 1m;
        }

        var need = distinct.Where(s => !Stablecoins.Contains(s)).ToList();
        if (need.Count == 0)
            return result;

        var baseUrl = _config["PriceProvider:CoinGeckoBaseUrl"] ?? "https://api.coingecko.com/api/v3";
        var isTodayUtc = asOfDate.Date == DateTime.UtcNow.Date;

        if (isTodayUtc)
        {
            var symToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sym in need)
            {
                var id = await ResolveCoinGeckoIdAsync(sym, baseUrl, cancellationToken);
                if (id != null)
                    symToId[sym] = id;
            }

            var uniqueIds = symToId.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            const int chunkSize = 40;
            for (var i = 0; i < uniqueIds.Count; i += chunkSize)
            {
                var chunk = uniqueIds.Skip(i).Take(chunkSize).ToList();
                var idsParam = string.Join(',', chunk.Select(Uri.EscapeDataString));
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                try
                {
                    var res = await client.GetAsync($"{baseUrl}/simple/price?ids={idsParam}&vs_currencies=usd", cancellationToken);
                    if (!res.IsSuccessStatusCode)
                        continue;
                    var json = await res.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var geckoId = prop.Name;
                        if (!prop.Value.TryGetProperty("usd", out var usdEl))
                            continue;
                        if (!usdEl.TryGetDecimal(out var price) || price <= 0)
                            continue;
                        foreach (var kv in symToId)
                        {
                            if (string.Equals(kv.Value, geckoId, StringComparison.OrdinalIgnoreCase))
                                result[kv.Key] = price;
                        }
                    }
                }
                catch
                {
                    // Try next chunk / fallbacks
                }
            }
        }

        foreach (var sym in need)
        {
            if (result[sym].HasValue)
                continue;
            result[sym] = await GetPriceInUsdAsync(sym, asOfDate, cancellationToken);
            try
            {
                await Task.Delay(400, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return result;
    }

    public async Task<decimal?> GetPriceInUsdAsync(string symbol, DateTime date, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;
        symbol = symbol.Trim();
        if (Stablecoins.Contains(symbol))
            return 1m;

        var baseUrl = _config["PriceProvider:CoinGeckoBaseUrl"] ?? "https://api.coingecko.com/api/v3";

        if (date.Date == DateTime.UtcNow.Date)
        {
            var id = await ResolveCoinGeckoIdAsync(symbol, baseUrl, cancellationToken);
            if (id != null)
            {
                var simple = await TryCoinGeckoSimplePriceAsync(id, baseUrl, cancellationToken);
                if (simple.HasValue)
                    return simple.Value;
            }
        }

        var coingecko = await TryCoinGeckoHistoryAsync(symbol, date, baseUrl, cancellationToken);
        if (coingecko.HasValue)
            return coingecko.Value;

        return await TryCryptoCompareAsync(symbol, date, cancellationToken);
    }

    private async Task<decimal?> TryCoinGeckoSimplePriceAsync(string id, string baseUrl, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        try
        {
            var res = await client.GetAsync($"{baseUrl}/simple/price?ids={Uri.EscapeDataString(id)}&vs_currencies=usd", ct);
            if (!res.IsSuccessStatusCode)
                return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(id, out var coin) || !coin.TryGetProperty("usd", out var usd))
                return null;
            if (usd.TryGetDecimal(out var price) && price > 0)
                return price;
        }
        catch
        {
            // fall through
        }
        return null;
    }

    private async Task<decimal?> TryCoinGeckoHistoryAsync(string symbol, DateTime date, string baseUrl, CancellationToken ct)
    {
        var id = await ResolveCoinGeckoIdAsync(symbol, baseUrl, ct);
        if (id == null)
            return null;

        var dateStr = date.ToString("dd-MM-yyyy");
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            var res = await client.GetAsync($"{baseUrl}/coins/{Uri.EscapeDataString(id)}/history?date={dateStr}", ct);
            if (!res.IsSuccessStatusCode)
                return null;
            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("market_data", out var md) &&
                md.TryGetProperty("current_price", out var cp) &&
                cp.TryGetProperty("usd", out var usd))
            {
                if (usd.TryGetDecimal(out var price) && price > 0)
                    return price;
            }
        }
        catch
        {
            // Fall through to CryptoCompare
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

    private static async Task EnsureCoinGeckoListFetchedAsync(string baseUrl, CancellationToken ct)
    {
        if (SymbolToId.Count > 0)
            return;
        await ListLock.WaitAsync(ct);
        try
        {
            if (SymbolToId.Count > 0)
                return;
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var res = await client.GetAsync($"{baseUrl}/coins/list", ct);
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
        var baseUrl = _config["PriceProvider:CryptoCompareBaseUrl"] ?? "https://min-api.cryptocompare.com";
        var ts = new DateTimeOffset(date.Date).ToUnixTimeSeconds();
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        var apiKey = _config["PriceProvider:CryptoCompareApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
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
                symEl.TryGetProperty("USD", out var usd))
            {
                if (usd.TryGetDecimal(out var price) && price > 0)
                    return price;
            }
        }
        catch
        {
            // Ignore
        }
        return null;
    }
}
