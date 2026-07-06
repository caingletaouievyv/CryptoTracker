using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoTracker.DTOs;
using CryptoTracker.Exceptions;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CryptoTracker.Services;

public class OkxService : IOkxService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;

    public OkxService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public async Task<IReadOnlyList<OkxBillItem>> FetchBillsForSyncAsync(
        int limit,
        OkxCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var merged = new Dictionary<string, OkxBillItem>(StringComparer.Ordinal);

        AddBills(merged, await GetBillsPageAsync("/api/v5/account/bills", null, limit, credentials, cancellationToken));

        string? cursor = null;
        for (var page = 0; page < 50; page++)
        {
            var batch = await GetBillsPageAsync("/api/v5/account/bills-archive", cursor, limit, credentials, cancellationToken);
            if (batch.Count == 0) break;
            AddBills(merged, batch);
            if (batch.Count < limit) break;
            cursor = batch[^1].BillId;
            if (string.IsNullOrEmpty(cursor)) break;
        }

        return merged.Values.ToList();
    }

    private static void AddBills(Dictionary<string, OkxBillItem> merged, IReadOnlyList<OkxBillItem> batch)
    {
        foreach (var b in batch)
        {
            if (!string.IsNullOrEmpty(b.BillId))
                merged[b.BillId] = b;
        }
    }

    private async Task<IReadOnlyList<OkxBillItem>> GetBillsPageAsync(
        string endpoint,
        string? after,
        int limit,
        OkxCredentials credentials,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuth(credentials, out var key, out var secret, out var passphrase))
            return Array.Empty<OkxBillItem>();

        var query = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrEmpty(after))
            query.Add($"after={Uri.EscapeDataString(after)}");

        var queryStr = string.Join("&", query);
        var requestPath = $"{endpoint}?{queryStr}";
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(requestPath));
        Sign(request, "GET", requestPath, null, key, secret, passphrase);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new AppHttpException(StatusCodes.Status502BadGateway,
                $"OKX HTTP {(int)response.StatusCode}. Check Okx:BaseUrl (default https://www.okx.com) and network access.");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetString() : null;
        var msg = root.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : null;

        if (code != "0")
        {
            throw new AppHttpException(StatusCodes.Status502BadGateway,
                string.IsNullOrWhiteSpace(msg)
                    ? $"OKX API error (code {code ?? "?"}). Check API key Read permission and passphrase."
                    : $"OKX API: {msg} (code {code})");
        }

        if (!root.TryGetProperty("data", out var data))
            return Array.Empty<OkxBillItem>();

        var list = new List<OkxBillItem>();
        foreach (var b in data.EnumerateArray())
        {
            list.Add(new OkxBillItem
            {
                BillId = b.TryGetProperty("billId", out var bi) ? bi.GetString() ?? "" : "",
                Ccy = b.TryGetProperty("ccy", out var c) ? c.GetString() ?? "" : "",
                Type = b.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "",
                SubType = b.TryGetProperty("subType", out var st) ? st.GetString() ?? "" : "",
                Ts = b.TryGetProperty("ts", out var tsProp) ? tsProp.GetString() ?? "" : "",
                Sz = b.TryGetProperty("sz", out var szProp) && decimal.TryParse(szProp.GetString(), out var szVal) ? szVal : 0m,
                Px = b.TryGetProperty("px", out var pxProp) && decimal.TryParse(pxProp.GetString(), out var pxVal) ? pxVal : 0m,
                Fee = b.TryGetProperty("fee", out var feeProp) && decimal.TryParse(feeProp.GetString(), out var feeVal) ? feeVal : 0m,
                InstId = b.TryGetProperty("instId", out var ii) ? ii.GetString() ?? "" : "",
            });
        }

        return list;
    }

    private bool TryGetAuth(OkxCredentials? overrides, out string apiKey, out string secret, out string passphrase)
    {
        if (overrides != null && overrides.IsComplete)
        {
            apiKey = overrides.ApiKey.Trim();
            secret = overrides.SecretKey.Trim();
            passphrase = overrides.Passphrase.Trim();
            return true;
        }

        apiKey = _config["Okx:ApiKey"] ?? "";
        secret = _config["Okx:SecretKey"] ?? "";
        passphrase = _config["Okx:Passphrase"] ?? "";
        return !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(passphrase);
    }

    private Uri BuildUri(string path)
    {
        var baseUrl = (_config["Okx:BaseUrl"] ?? "https://www.okx.com").TrimEnd('/');
        return new Uri(baseUrl + path, UriKind.Absolute);
    }

    private void Sign(HttpRequestMessage request, string method, string requestPath, string? body, string apiKey, string secret, string passphrase)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var prehash = timestamp + method + requestPath + (body ?? "");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(prehash));
        var sign = Convert.ToBase64String(hash);

        request.Headers.TryAddWithoutValidation("OK-ACCESS-KEY", apiKey);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-SIGN", sign);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-TIMESTAMP", timestamp);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-PASSPHRASE", passphrase);
    }
}
