using CryptoTracker.DTOs;
using CryptoTracker.Exceptions;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CryptoTracker.Services;

public class OkxSyncService : IOkxSyncService
{
    private readonly IOkxService _okxService;
    private readonly ITransactionService _transactionService;

    public OkxSyncService(IOkxService okxService, ITransactionService transactionService)
    {
        _okxService = okxService;
        _transactionService = transactionService;
    }

    public async Task<OkxSyncResultDto> SyncOkxSpotTransactionsAsync(OkxSyncTransactionsRequest? body, CancellationToken cancellationToken = default)
    {
        var creds = ToCredentials(body);
        if (creds == null)
            throw new ArgumentException("Provide OKX credentials in the request body (apiKey, secretKey, passphrase).");

        var after = body?.After;
        var limit = Math.Clamp(body?.Limit ?? 100, 1, 100);
        IReadOnlyList<OkxBillItem> bills;
        try
        {
            bills = await _okxService.GetBillsAsync(after, limit, creds, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new AppHttpException(StatusCodes.Status502BadGateway,
                "Cannot reach OKX. Check your network and that www.okx.com is not blocked (firewall, DNS, or region).");
        }

        if (bills.Count == 0)
        {
            return new OkxSyncResultDto
            {
                Synced = 0,
                Updated = 0,
                Message = "No SPOT trade bills in this range. OKX returns up to ~3 months of trading bills; ensure your API key has Read permission for Trading. For Earn/Funding activity, use CSV import or add transactions manually."
            };
        }

        var requests = new List<CreateTransactionRequest>();
        foreach (var b in bills)
        {
            var type = MapOkxSubTypeToType(b.SubType);
            if (type == null) continue;

            if (!long.TryParse(b.Ts, out var ms)) continue;
            var date = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
            var baseCcy = GetBaseFromInstId(b.InstId);
            var fee = b.Fee < 0 ? -b.Fee : 0m;

            requests.Add(new CreateTransactionRequest
            {
                Symbol = b.Ccy,
                Type = type,
                Quantity = b.Sz,
                PriceAtTransaction = b.Px > 0 ? b.Px : 0,
                Fee = fee,
                Date = date,
                BaseCurrency = baseCcy,
                Notes = string.IsNullOrEmpty(b.BillId) ? null : "OKX:" + b.BillId
            });
        }

        if (requests.Count == 0)
        {
            return new OkxSyncResultDto
            {
                Synced = 0,
                Updated = 0,
                Message = "No Buy/Sell bills in this page."
            };
        }

        var synced = await _transactionService.AddTransactionsAsync(requests, cancellationToken);
        var updated = await _transactionService.BackfillPricesAsync(cancellationToken);
        return new OkxSyncResultDto
        {
            Synced = synced,
            Updated = updated,
            Message = "Transactions synced from OKX bills; prices backfilled where missing."
        };
    }

    private static string? MapOkxSubTypeToType(string subType) =>
        subType switch
        {
            "1" => "Buy",
            "2" => "Sell",
            _ => null
        };

    private static string GetBaseFromInstId(string instId)
    {
        if (string.IsNullOrEmpty(instId)) return "USDT";
        var parts = instId.Split('-');
        return parts.Length >= 2 ? parts[^1] : "USDT";
    }

    private static OkxCredentials? ToCredentials(OkxSyncTransactionsRequest? body) =>
        ToCredentials(body?.ApiKey, body?.SecretKey, body?.Passphrase);

    private static OkxCredentials? ToCredentials(string? apiKey, string? secretKey, string? passphrase)
    {
        var a = apiKey?.Trim() ?? "";
        var s = secretKey?.Trim() ?? "";
        var p = passphrase?.Trim() ?? "";
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(s) || string.IsNullOrEmpty(p)) return null;
        return new OkxCredentials { ApiKey = a, SecretKey = s, Passphrase = p };
    }
}
