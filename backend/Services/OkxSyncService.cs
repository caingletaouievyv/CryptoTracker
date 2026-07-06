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

        var limit = Math.Clamp(body?.Limit ?? 100, 1, 100);
        IReadOnlyList<OkxBillItem> allBills;
        try
        {
            allBills = await _okxService.FetchBillsForSyncAsync(limit, creds, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new AppHttpException(StatusCodes.Status502BadGateway,
                "Cannot reach OKX. Check your network and that www.okx.com is not blocked (firewall, DNS, or region).");
        }

        var bills = allBills.Where(IsSyncableBill).ToList();

        if (bills.Count == 0)
        {
            return new OkxSyncResultDto
            {
                Synced = 0,
                Updated = 0,
                Message = BuildEmptyMessage(allBills),
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
                Notes = string.IsNullOrEmpty(b.BillId) ? null : "OKX:" + b.BillId,
            });
        }

        if (requests.Count == 0)
        {
            return new OkxSyncResultDto
            {
                Synced = 0,
                Updated = 0,
                Message = $"OKX returned {bills.Count} trade/convert bill(s) but none mapped to Buy/Sell. Check OKX bill subtypes.",
            };
        }

        var synced = await _transactionService.AddTransactionsAsync(requests, cancellationToken);
        var updated = await _transactionService.BackfillPricesAsync(cancellationToken);
        return new OkxSyncResultDto
        {
            Synced = synced,
            Updated = updated,
            Message = "Transactions synced from OKX (SPOT trades, Convert, Simple trade); prices backfilled where missing.",
        };
    }

    private static string BuildEmptyMessage(IReadOnlyList<OkxBillItem> allBills)
    {
        if (allBills.Count == 0)
        {
            return "OKX returned no bills. Confirm the API key has Read permission, passphrase is correct, and the key belongs to this account. Transfer/Funding-only activity is not imported. Older than ~3 months: use seed scripts.";
        }

        var types = string.Join(", ", allBills.Select(b => b.Type).Distinct().OrderBy(t => t).Take(6));
        return $"OKX returned {allBills.Count} bill(s) (types: {types}) but none were SPOT trade, Convert, or Simple trade. Transfer rows are skipped.";
    }

    private static bool IsSyncableBill(OkxBillItem b) => b.Type is "2" or "27" or "30";

    /// <summary>OKX bill subTypes → ledger type. See OKX GET /api/v5/account/subtypes.</summary>
    private static string? MapOkxSubTypeToType(string subType) =>
        subType switch
        {
            "1" => "Buy",
            "2" => "Sell",
            "318" => "Buy",
            "319" => "Sell",
            "320" => "Buy",
            "321" => "Sell",
            "236" => "Buy",
            "237" => "Sell",
            _ => null,
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
