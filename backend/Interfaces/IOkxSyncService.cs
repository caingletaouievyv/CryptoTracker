using CryptoTracker.DTOs;

namespace CryptoTracker.Interfaces;

public interface IOkxSyncService
{
    Task<OkxSyncResultDto> SyncOkxSpotTransactionsAsync(OkxSyncTransactionsRequest? body, CancellationToken cancellationToken = default);
}
