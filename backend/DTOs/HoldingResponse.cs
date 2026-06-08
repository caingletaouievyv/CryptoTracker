namespace CryptoTracker.DTOs;

public class HoldingResponse
{
    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public string Source { get; set; } = "Earn";
    public DateTime LastUpdated { get; set; }
    public decimal? SellTargetUsd { get; set; }
    public decimal? BuyZoneUsd { get; set; }
}
