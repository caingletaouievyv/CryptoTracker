namespace CryptoTracker.DTOs;

/// <summary>Optional OKX credentials + pagination for transaction sync.</summary>
public class OkxSyncTransactionsRequest
{
    public string? ApiKey { get; set; }
    public string? SecretKey { get; set; }
    public string? Passphrase { get; set; }
    public string? After { get; set; }
    public int? Limit { get; set; }
}
