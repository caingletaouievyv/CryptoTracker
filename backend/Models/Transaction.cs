namespace CryptoTracker.Models;

public class Transaction
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Buy / Swap
    public decimal Quantity { get; set; }
    public decimal PriceAtTransaction { get; set; }
    public decimal Fee { get; set; }
    public DateTime Date { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    /// <summary>Optional: e.g. staking, airdrop, transfer.</summary>
    public string? Notes { get; set; }
}
