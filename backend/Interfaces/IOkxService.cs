using CryptoTracker.DTOs;



namespace CryptoTracker.Interfaces;



public interface IOkxService

{

    /// <summary>Recent (7d) + archive (3mo) bills, deduped. Caller filters bill types.</summary>

    Task<IReadOnlyList<OkxBillItem>> FetchBillsForSyncAsync(int limit, OkxCredentials credentials, CancellationToken cancellationToken = default);

}

