namespace CryptoTracker.DTOs;

/// <summary>Credentials for one OKX request. When null/incomplete, OkxService uses config (Okx:*).</summary>
public class OkxCredentials
{
    public string ApiKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
    public string Passphrase { get; init; } = "";
    public bool IsComplete => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(Passphrase);
}

/// <summary>One row from OKX v5 account bills (SPOT trade).</summary>
public class OkxBillItem
{
    public string BillId { get; set; } = string.Empty;
    public string Ccy { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SubType { get; set; } = string.Empty;
    public string Ts { get; set; } = string.Empty;
    public decimal Sz { get; set; }
    public decimal Px { get; set; }
    public decimal Fee { get; set; }
    public string InstId { get; set; } = string.Empty;
}
