using CryptoTracker.DTOs;

namespace CryptoTracker.Interfaces;

public interface IOkxService
{
    Task<IReadOnlyList<OkxBillItem>> GetBillsAsync(string? after = null, int limit = 100, OkxCredentials? credentials = null, CancellationToken cancellationToken = default);
}
