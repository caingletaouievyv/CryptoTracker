using CryptoTracker.DTOs;

namespace CryptoTracker.Interfaces;

public interface IHoldingService
{
    Task<IReadOnlyList<HoldingResponse>> GetHoldingsAsync(CancellationToken cancellationToken = default);

    Task<PortfolioDashboardResponse> GetPortfolioAsync(CancellationToken cancellationToken = default);

    Task SetHoldingsAsync(IReadOnlyList<HoldingItem> items, CancellationToken cancellationToken = default);
}
