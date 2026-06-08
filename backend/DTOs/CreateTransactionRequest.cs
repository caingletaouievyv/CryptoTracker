using System.ComponentModel.DataAnnotations;

namespace CryptoTracker.DTOs;

public class CreateTransactionRequest
{
    [Required, MinLength(1), MaxLength(50)]
    public string Symbol { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Type { get; set; } = string.Empty; // Buy, Sell, Swap, Fee, Deposit, Withdraw

    /// <summary>Can be negative (e.g. Fee, Withdraw). Validated in service: cannot be zero.</summary>
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
    public decimal PriceAtTransaction { get; set; }

    /// <summary>Positive = fee-out (cost), negative = fee-in (credit).</summary>
    public decimal Fee { get; set; }

    [Required]
    public DateTime Date { get; set; }

    [MaxLength(20)]
    public string BaseCurrency { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Notes { get; set; }
}
