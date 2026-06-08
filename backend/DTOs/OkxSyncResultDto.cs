namespace CryptoTracker.DTOs;

public class OkxSyncResultDto
{
    public int Synced { get; init; }
    public int Updated { get; init; }
    public string? Message { get; init; }
}
