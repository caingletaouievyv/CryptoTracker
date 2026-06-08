namespace CryptoTracker.Interfaces;

public interface IPriceService
{
    Task<decimal?> GetPriceInUsdAsync(string symbol, DateTime date, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, decimal?>> GetSpotPricesUsdAsync(
        IReadOnlyList<string> symbols,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);
}
