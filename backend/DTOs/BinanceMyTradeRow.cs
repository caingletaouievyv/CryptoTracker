using System.Text.Json.Serialization;

namespace CryptoTracker.DTOs;

/// <summary>One row from GET /api/v3/myTrades.</summary>
public class BinanceMyTradeRow
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("price")] public string Price { get; set; } = "";
    [JsonPropertyName("qty")] public string Qty { get; set; } = "";
    [JsonPropertyName("quoteQty")] public string QuoteQty { get; set; } = "";
    [JsonPropertyName("commission")] public string Commission { get; set; } = "";
    [JsonPropertyName("commissionAsset")] public string CommissionAsset { get; set; } = "";
    [JsonPropertyName("time")] public long Time { get; set; }
    [JsonPropertyName("isBuyer")] public bool IsBuyer { get; set; }
    [JsonPropertyName("symbol")] public string Symbol { get; set; } = "";
}
