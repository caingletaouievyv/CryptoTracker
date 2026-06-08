namespace CryptoTracker.Models;

public class Holding
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public string Source { get; set; } = "Earn";
    public DateTime LastUpdated { get; set; }

    /// <summary>Optional USD price target for status &quot;READY TO SELL&quot;.</summary>
    public decimal? SellTargetUsd { get; set; }

    /// <summary>Optional USD max price for &quot;ACCUMULATION ZONE&quot; (price at or below = in zone).</summary>
    public decimal? BuyZoneUsd { get; set; }
}
