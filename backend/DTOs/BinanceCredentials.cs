namespace CryptoTracker.DTOs;

public class BinanceCredentials
{
    public string ApiKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
    public bool IsComplete => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(SecretKey);
}
