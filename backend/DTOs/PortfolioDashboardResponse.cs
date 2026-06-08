namespace CryptoTracker.DTOs;

/// <summary>GET /api/portfolio — positions plus rollups. Quantity/cost rules: docs/project.md.</summary>
public class PortfolioDashboardResponse
{
    public List<PortfolioPositionResponse> Positions { get; set; } = new();
    public PortfolioSummaryDto Summary { get; set; } = new();
}

public class PortfolioSummaryDto
{
    /// <summary>Sum of position market values in USD (null prices treated as 0).</summary>
    public decimal TotalValueUsd { get; set; }

    /// <summary>Sum of cost basis for displayed positions.</summary>
    public decimal TotalCostBasis { get; set; }

    /// <summary>Sum of unrealized P/L for displayed positions.</summary>
    public decimal TotalUnrealizedPnl { get; set; }

    /// <summary>Sum of |qty|×price+fee for Sell rows with price &gt; 0 (proceeds; not net of cost).</summary>
    public decimal RealizedSellProceedsUsd { get; set; }
}
