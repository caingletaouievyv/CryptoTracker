namespace CryptoTracker.DTOs;

public class PortfolioPositionResponse
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? Source { get; set; }
    public decimal TotalCostBasis { get; set; }
    public decimal AveragePricePerUnit { get; set; }

    /// <summary>Spot USD price used for value/PnL (today, UTC date).</summary>
    public decimal? CurrentPriceUsd { get; set; }

    /// <summary>Quantity × CurrentPriceUsd when price known.</summary>
    public decimal? CurrentValueUsd { get; set; }

    public decimal? UnrealizedPnl { get; set; }

    /// <summary>Share of total portfolio value (0–100).</summary>
    public decimal? AllocationPercent { get; set; }

    /// <summary>WAITING | ACCUMULATION ZONE | READY TO SELL — from optional targets on holding.</summary>
    public string? StrategyStatus { get; set; }
}
