using System.ComponentModel.DataAnnotations;

namespace CryptoTracker.DTOs;

public class BulkTransactionsRequest
{
    [Required, MinLength(1), MaxLength(5000)]
    public List<CreateTransactionRequest> Transactions { get; set; } = new();
}
