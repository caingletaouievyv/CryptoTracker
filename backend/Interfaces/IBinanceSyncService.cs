using CryptoTracker.DTOs;

namespace CryptoTracker.Interfaces;

public interface IBinanceSyncService
{
    Task<OkxSyncResultDto> SyncBinanceSpotTradesAsync(BinanceSyncTransactionsRequest? body, CancellationToken cancellationToken = default);
}
