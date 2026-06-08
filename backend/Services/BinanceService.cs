using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;

namespace CryptoTracker.Services;

public class BinanceService : IBinanceService
{
    private const int MyTradesLimit = 1000;
    private const int MaxPagesPerSymbol = 80;
    private const int MyTradesWeight = 20;

    /// <summary>Serializes myTrades calls process-wide so spacing + backoff stay under Binance weight limits.</summary>
    private static readonly SemaphoreSlim MyTradesGate = new(1, 1);
    private static long _nextMyTradesAllowedUtcTicks;

    private static readonly SemaphoreSlim ExchangeInfoGate = new(1, 1);
    private static IReadOnlyDictionary<string, (string Base, string Quote)>? _symbolMapCache;
    private static DateTime _symbolMapUtc = DateTime.MinValue;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<BinanceService> _logger;

    public BinanceService(HttpClient httpClient, IConfiguration config, ILogger<BinanceService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, (string BaseAsset, string QuoteAsset)>> GetTradingSymbolMapAsync(
        CancellationToken cancellationToken = default)
    {
        if (_symbolMapCache != null && DateTime.UtcNow - _symbolMapUtc < TimeSpan.FromHours(1))
            return _symbolMapCache;

        await ExchangeInfoGate.WaitAsync(cancellationToken);
        try
        {
            if (_symbolMapCache != null && DateTime.UtcNow - _symbolMapUtc < TimeSpan.FromHours(1))
                return _symbolMapCache;

            var url = $"{GetBaseUrl()}/api/v3/exchangeInfo";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Binance exchangeInfo failed: {Status} {Body}", response.StatusCode, json);
                return _symbolMapCache ?? new Dictionary<string, (string, string)>();
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var doc = JsonSerializer.Deserialize<ExchangeInfoEnvelope>(json, options);
            var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
            if (doc?.Symbols != null)
            {
                foreach (var s in doc.Symbols)
                {
                    if (string.IsNullOrEmpty(s.Symbol) || !string.Equals(s.Status, "TRADING", StringComparison.OrdinalIgnoreCase))
                        continue;
                    map[s.Symbol.ToUpperInvariant()] = (s.BaseAsset ?? "", s.QuoteAsset ?? "");
                }
            }

            _symbolMapCache = map;
            _symbolMapUtc = DateTime.UtcNow;
            return map;
        }
        finally
        {
            ExchangeInfoGate.Release();
        }
    }

    public async Task<IReadOnlyList<BinanceMyTradeRow>> GetAllMyTradesAsync(
        string symbol,
        BinanceCredentials credentials,
        int historyLookbackDays,
        CancellationToken cancellationToken = default)
    {
        if (!credentials.IsComplete)
            return Array.Empty<BinanceMyTradeRow>();

        var sym = symbol.Trim().ToUpperInvariant();
        var cap = Math.Clamp(_config.GetValue("Binance:HistoryLookbackMaxDays", 3650), 1, 3650);
        historyLookbackDays = Math.Clamp(historyLookbackDays, 1, cap);

        var all = new List<BinanceMyTradeRow>();
        long? endTimeMs = null;

        for (var page = 0; page < MaxPagesPerSymbol; page++)
        {
            var batch = await FetchMyTradesRecentPageAsync(sym, endTimeMs, credentials, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
                break;

            all.InsertRange(0, batch);
            if (batch.Count < MyTradesLimit)
                break;

            endTimeMs = batch[0].Time - 1;
        }

        // Binance often returns [] for symbol-only when trades are outside the "recent" slice; myTrades allows startTime+endTime only in ≤24h ranges.
        if (all.Count == 0)
        {
            _logger.LogInformation("Binance myTrades empty for {Symbol}; scanning last {Days} days in 24h windows.", sym, historyLookbackDays);
            var scanned = await CollectTradesBy24HourWindowsAsync(sym, credentials, historyLookbackDays, cancellationToken).ConfigureAwait(false);
            return scanned;
        }

        return all;
    }

    /// <summary>Binance: startTime+endTime span must not exceed 24 hours.</summary>
    private async Task<IReadOnlyList<BinanceMyTradeRow>> CollectTradesBy24HourWindowsAsync(
        string symbol,
        BinanceCredentials credentials,
        int lookbackDays,
        CancellationToken cancellationToken)
    {
        const long msPerDay = 86_400_000L;
        var seen = new HashSet<long>();
        var acc = new List<BinanceMyTradeRow>();
        var endW = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var minMs = endW - (long)lookbackDays * msPerDay;

        while (endW >= minMs)
        {
            var startW = Math.Max(minMs, endW - msPerDay + 1);
            // When endW reaches minMs, startW clamps to minMs → zero-width window; skip (Binance would get startTime==endTime).
            if (startW >= endW)
                break;

            var batch = await FetchMyTradesWindowAsync(symbol, startW, endW, credentials, cancellationToken).ConfigureAwait(false);
            foreach (var t in batch)
            {
                if (seen.Add(t.Id))
                    acc.Add(t);
            }

            if (batch.Count >= MyTradesLimit)
            {
                _logger.LogWarning(
                    "Binance myTrades returned {Limit} rows for {Symbol} in one 24h window; some fills may be missing for that window.",
                    MyTradesLimit, symbol);
            }

            endW = startW - 1;
        }

        acc.Sort((a, b) => a.Time.CompareTo(b.Time));
        return acc;
    }

    private async Task<List<BinanceMyTradeRow>> FetchMyTradesRecentPageAsync(
        string symbol,
        long? endTimeMs,
        BinanceCredentials credentials,
        CancellationToken cancellationToken)
    {
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["limit"] = MyTradesLimit.ToString(),
            ["recvWindow"] = "60000",
            ["symbol"] = symbol,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
        };
        if (endTimeMs.HasValue)
            p["endTime"] = endTimeMs.Value.ToString();

        return await SendMyTradesAsync(p, credentials, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<BinanceMyTradeRow>> FetchMyTradesWindowAsync(
        string symbol,
        long startTimeMs,
        long endTimeMs,
        BinanceCredentials credentials,
        CancellationToken cancellationToken)
    {
        var p = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["endTime"] = endTimeMs.ToString(),
            ["limit"] = MyTradesLimit.ToString(),
            ["recvWindow"] = "60000",
            ["startTime"] = startTimeMs.ToString(),
            ["symbol"] = symbol,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
        };

        return await SendMyTradesAsync(p, credentials, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<BinanceMyTradeRow>> SendMyTradesAsync(
        SortedDictionary<string, string> parameters,
        BinanceCredentials credentials,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Clamp(_config.GetValue("Binance:MyTradesMaxRetries", 4), 1, 8);
        var retrySeconds = Math.Clamp(_config.GetValue("Binance:MyTradesRateLimitRetrySeconds", 65), 30, 120);
        var intervalMs = Math.Clamp(_config.GetValue("Binance:MyTradesRequestIntervalMs", 0), 0, 10_000);
        if (intervalMs <= 0)
        {
            var perMinute = _config.GetValue("Binance:MyTradesWeightLimitPerMinute", 6000);
            intervalMs = Math.Max(50, (int)Math.Ceiling(60_000.0 / Math.Max(1, perMinute / MyTradesWeight)) + 15);
        }

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await MyTradesGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var nextAllowed = new DateTime(Volatile.Read(ref _nextMyTradesAllowedUtcTicks), DateTimeKind.Utc);
                if (_nextMyTradesAllowedUtcTicks != 0 && now < nextAllowed)
                    await Task.Delay(nextAllowed - now, cancellationToken).ConfigureAwait(false);

                var query = BuildSignedQuery(parameters, credentials.SecretKey.Trim());
                var url = $"{GetBaseUrl()}/api/v3/myTrades?{query}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("X-MBX-APIKEY", credentials.ApiKey.Trim());

                using var response = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                Volatile.Write(ref _nextMyTradesAllowedUtcTicks, DateTime.UtcNow.AddMilliseconds(intervalMs).Ticks);

                var rateLimited = IsBinanceWeightLimit(response.StatusCode, json);
                if (rateLimited && attempt < maxAttempts - 1)
                {
                    _logger.LogWarning("Binance myTrades weight limit (attempt {Attempt}/{Max}); waiting {Seconds}s.", attempt + 1, maxAttempts, retrySeconds);
                    Volatile.Write(ref _nextMyTradesAllowedUtcTicks, DateTime.UtcNow.AddSeconds(retrySeconds).Ticks);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var msg = TryParseBinanceError(json) ?? $"Binance HTTP {(int)response.StatusCode}";
                    throw new ArgumentException(msg);
                }

                if (TryGetBinanceError(json, out var code, out var errMsg) && code < 0)
                {
                    if (code == -1003 && attempt < maxAttempts - 1)
                    {
                        _logger.LogWarning("Binance code {Code}; waiting {Seconds}s before retry.", code, retrySeconds);
                        Volatile.Write(ref _nextMyTradesAllowedUtcTicks, DateTime.UtcNow.AddSeconds(retrySeconds).Ticks);
                        continue;
                    }
                    throw new ArgumentException(errMsg ?? $"Binance error {code}");
                }

                var rows = JsonSerializer.Deserialize<List<BinanceMyTradeRow>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                return rows ?? new List<BinanceMyTradeRow>();
            }
            finally
            {
                MyTradesGate.Release();
            }
        }

        throw new InvalidOperationException("Binance myTrades: retries exhausted.");
    }

    private static bool IsBinanceWeightLimit(HttpStatusCode status, string json)
    {
        if (status == HttpStatusCode.TooManyRequests)
            return true;
        if (TryGetBinanceError(json, out var code, out _) && code == -1003)
            return true;
        return json.Contains("Too much request weight", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetBinanceError(string json, out int code, out string? message)
    {
        code = 0;
        message = null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty("code", out var c) || c.ValueKind != JsonValueKind.Number)
                return false;
            code = c.GetInt32();
            if (root.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String)
                message = m.GetString();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildSignedQuery(SortedDictionary<string, string> parameters, string secretKey)
    {
        var qs = string.Join("&", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(qs))).ToLowerInvariant();
        return qs + "&signature=" + sig;
    }

    private static string? TryParseBinanceError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("msg", out var m))
                return m.GetString();
        }
        catch (JsonException)
        {
            /* ignore */
        }
        return null;
    }

    private string GetBaseUrl() => (_config["Binance:BaseUrl"] ?? "https://api.binance.com").TrimEnd('/');

    private sealed class ExchangeInfoEnvelope
    {
        [JsonPropertyName("symbols")] public List<ExchangeSymbolRow>? Symbols { get; set; }
    }

    private sealed class ExchangeSymbolRow
    {
        [JsonPropertyName("symbol")] public string? Symbol { get; set; }
        [JsonPropertyName("baseAsset")] public string? BaseAsset { get; set; }
        [JsonPropertyName("quoteAsset")] public string? QuoteAsset { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
    }
}
