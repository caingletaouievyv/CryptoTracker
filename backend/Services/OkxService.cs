using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;

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

    public async Task<IReadOnlyList<OkxBillItem>> GetBillsAsync(string? after = null, int limit = 100, OkxCredentials? credentials = null, CancellationToken cancellationToken = default)
    {
        if (!TryGetAuth(credentials, out var key, out var secret, out var passphrase))
            return Array.Empty<OkxBillItem>();

        var query = new List<string> { "instType=SPOT", "type=2", $"limit={Math.Clamp(limit, 1, 100)}" };
        if (!string.IsNullOrEmpty(after)) query.Add($"after={after}");
        var queryStr = string.Join("&", query);
        var requestPath = "/api/v5/account/bills?" + queryStr;
        var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(requestPath));
        Sign(request, "GET", "/api/v5/account/bills?" + queryStr, null, key, secret, passphrase);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Array.Empty<OkxBillItem>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("code", out var code) && code.GetString() != "0")
            return Array.Empty<OkxBillItem>();
        if (!root.TryGetProperty("data", out var data))
            return Array.Empty<OkxBillItem>();

        var list = new List<OkxBillItem>();
        foreach (var b in data.EnumerateArray())
        {
            var billId = b.TryGetProperty("billId", out var bi) ? bi.GetString() ?? "" : "";
            var ccy = b.TryGetProperty("ccy", out var c) ? c.GetString() ?? "" : "";
            var type = b.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
            var subType = b.TryGetProperty("subType", out var st) ? st.GetString() ?? "" : "";
            var ts = b.TryGetProperty("ts", out var tsProp) ? tsProp.GetString() ?? "" : "";
            var sz = b.TryGetProperty("sz", out var szProp) && decimal.TryParse(szProp.GetString(), out var szVal) ? szVal : 0m;
            var px = b.TryGetProperty("px", out var pxProp) && decimal.TryParse(pxProp.GetString(), out var pxVal) ? pxVal : 0m;
            var fee = b.TryGetProperty("fee", out var feeProp) && decimal.TryParse(feeProp.GetString(), out var feeVal) ? feeVal : 0m;
            var instId = b.TryGetProperty("instId", out var ii) ? ii.GetString() ?? "" : "";
            list.Add(new OkxBillItem
            {
                BillId = billId,
                Ccy = ccy,
                Type = type,
                SubType = subType,
                Ts = ts,
                Sz = sz,
                Px = px,
                Fee = fee,
                InstId = instId
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
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(prehash));
        var sign = Convert.ToBase64String(hash);

        request.Headers.TryAddWithoutValidation("OK-ACCESS-KEY", apiKey);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-SIGN", sign);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-TIMESTAMP", timestamp);
        request.Headers.TryAddWithoutValidation("OK-ACCESS-PASSPHRASE", passphrase);
    }
}
