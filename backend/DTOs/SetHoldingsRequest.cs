namespace CryptoTracker.DTOs;

public class SetHoldingsRequest
{
    public List<HoldingItem> Holdings { get; set; } = new();
}

public class HoldingItem
{
    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public string Source { get; set; } = "Earn";

    /// <summary>Optional USD sell target (≥ price ⇒ READY TO SELL).</summary>
    public decimal? SellTargetUsd { get; set; }

    /// <summary>Optional USD accumulation ceiling (price ≤ ⇒ ACCUMULATION ZONE).</summary>
    public decimal? BuyZoneUsd { get; set; }
}
