using CryptoTracker.Data;
using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using CryptoTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class HoldingService : IHoldingService
{
    private const decimal MinDisplayQuantity = 1e-10m;

    private readonly AppDbContext _context;
    private readonly IPriceService _priceService;
    private readonly IConfiguration _config;
    private readonly ICurrentUser _currentUser;

    public HoldingService(AppDbContext context, IPriceService priceService, IConfiguration config, ICurrentUser currentUser)
    {
        _context = context;
        _priceService = priceService;
        _config = config;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<HoldingResponse>> GetHoldingsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var list = await _context.Holdings.Where(h => h.UserId == userId).OrderBy(h => h.Symbol).ToListAsync(cancellationToken);
        return list.Select(h => new HoldingResponse
        {
            Symbol = h.Symbol,
            CurrentQuantity = h.CurrentQuantity,
            Source = h.Source,
            LastUpdated = h.LastUpdated,
            SellTargetUsd = h.SellTargetUsd,
            BuyZoneUsd = h.BuyZoneUsd
        }).ToList();
    }

    public async Task<PortfolioDashboardResponse> GetPortfolioAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var holdings = await _context.Holdings
            .Where(h => h.UserId == userId && h.CurrentQuantity > MinDisplayQuantity)
            .ToListAsync(cancellationToken);

        var transactions = await _context.Transactions.Where(t => t.UserId == userId).ToListAsync(cancellationToken);

        var excluded = _config.GetSection("Portfolio:ExcludedSymbols").Get<string[]>() ?? Array.Empty<string>();
        var excludedSet = new HashSet<string>(excluded.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase);

        var includeOnly = _config.GetSection("Portfolio:IncludeSymbolsOnly").Get<string[]>();
        var includeSet = includeOnly is { Length: > 0 }
            ? new HashSet<string>(includeOnly.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()), StringComparer.OrdinalIgnoreCase)
            : null;

        var minNotional = _config.GetValue<decimal?>("Portfolio:MinNotionalUsd");

        var bySymbol = transactions
            .Where(t => string.Equals(t.Type, "Buy", StringComparison.OrdinalIgnoreCase) && t.PriceAtTransaction > 0 && t.Quantity > 0)
            .GroupBy(t => t.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g =>
            {
                var rows = g.ToList();
                var totalBuyQty = rows.Sum(t => t.Quantity);
                var totalCostBuys = rows.Sum(t => t.Quantity * t.PriceAtTransaction + t.Fee);
                return (TotalBuyQty: totalBuyQty, AvgPrice: totalBuyQty > 0 ? totalCostBuys / totalBuyQty : 0m);
            });

        var realizedSellProceedsUsd = transactions
            .Where(t => string.Equals(t.Type, "Sell", StringComparison.OrdinalIgnoreCase) && t.PriceAtTransaction > 0 && t.Quantity > 0)
            .Sum(t => t.Quantity * t.PriceAtTransaction + t.Fee);

        var today = DateTime.UtcNow.Date;
        var uniqueSymbols = holdings.Select(h => h.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var priceMap = await _priceService.GetSpotPricesUsdAsync(uniqueSymbols, today, cancellationToken);

        var rawPositions = holdings
            .OrderByDescending(h => h.CurrentQuantity)
            .Select(h =>
            {
                var avgPrice = bySymbol.TryGetValue(h.Symbol, out var s) ? s.AvgPrice : 0m;
                var costBasis = avgPrice * h.CurrentQuantity;
                priceMap.TryGetValue(h.Symbol, out var spot);
                var valueUsd = spot.HasValue ? h.CurrentQuantity * spot.Value : (decimal?)null;
                var unrealized = spot.HasValue && avgPrice > 0
                    ? (spot.Value - avgPrice) * h.CurrentQuantity
                    : (decimal?)null;

                return new PortfolioPositionResponse
                {
                    Symbol = h.Symbol,
                    Quantity = h.CurrentQuantity,
                    Source = h.Source,
                    TotalCostBasis = costBasis,
                    AveragePricePerUnit = avgPrice,
                    CurrentPriceUsd = spot,
                    CurrentValueUsd = valueUsd,
                    UnrealizedPnl = unrealized,
                    AllocationPercent = null,
                    StrategyStatus = ComputeStrategyStatus(spot, h.SellTargetUsd, h.BuyZoneUsd)
                };
            })
            .Where(p => !excludedSet.Contains(p.Symbol))
            .Where(p => includeSet == null || includeSet.Contains(p.Symbol))
            .ToList();

        if (minNotional is > 0)
        {
            rawPositions = rawPositions
                .Where(p => p.CurrentValueUsd >= minNotional || (p.CurrentValueUsd == null && p.TotalCostBasis >= minNotional))
                .ToList();
        }

        var totalValue = rawPositions.Sum(p => p.CurrentValueUsd ?? 0m);
        var totalCost = rawPositions.Sum(p => p.TotalCostBasis);
        var totalUnrealized = rawPositions.Sum(p => p.UnrealizedPnl ?? 0m);

        foreach (var p in rawPositions)
        {
            if (totalValue > 0 && p.CurrentValueUsd is { } v && v > 0)
                p.AllocationPercent = Math.Round(100m * v / totalValue, 2);
        }

        return new PortfolioDashboardResponse
        {
            Positions = rawPositions,
            Summary = new PortfolioSummaryDto
            {
                TotalValueUsd = totalValue,
                TotalCostBasis = totalCost,
                TotalUnrealizedPnl = totalUnrealized,
                RealizedSellProceedsUsd = realizedSellProceedsUsd
            }
        };
    }

    public async Task SetHoldingsAsync(IReadOnlyList<HoldingItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        var userId = _currentUser.RequireUserId();
        var now = DateTime.UtcNow;
        var existing = await _context.Holdings.Where(h => h.UserId == userId).ToListAsync(cancellationToken);
        _context.Holdings.RemoveRange(existing);
        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.Symbol)))
        {
            _context.Holdings.Add(new Holding
            {
                UserId = userId,
                Symbol = item.Symbol.Trim(),
                CurrentQuantity = item.CurrentQuantity,
                Source = string.IsNullOrWhiteSpace(item.Source) ? "Earn" : item.Source.Trim(),
                LastUpdated = now,
                SellTargetUsd = item.SellTargetUsd,
                BuyZoneUsd = item.BuyZoneUsd
            });
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string? ComputeStrategyStatus(decimal? spot, decimal? sellTargetUsd, decimal? buyZoneUsd)
    {
        if (!spot.HasValue)
            return null;
        var p = spot.Value;
        if (sellTargetUsd.HasValue && p >= sellTargetUsd.Value)
            return "READY TO SELL";
        if (buyZoneUsd.HasValue && p <= buyZoneUsd.Value)
            return "ACCUMULATION ZONE";
        return "WAITING";
    }
}
