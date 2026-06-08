namespace CryptoTracker.DTOs;

public class TransactionResponse
{
    public Guid Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal PriceAtTransaction { get; set; }
    public decimal Fee { get; set; }
    /// <summary>Computed: Quantity * PriceAtTransaction + Fee.</summary>
    public decimal NetValue { get; set; }
    public DateTime Date { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
