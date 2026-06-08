using CryptoTracker.Data;
using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using CryptoTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly IPriceService _priceService;
    private readonly ICurrentUser _currentUser;

    public TransactionService(AppDbContext context, IPriceService priceService, ICurrentUser currentUser)
    {
        _context = context;
        _priceService = priceService;
        _currentUser = currentUser;
    }

    public async Task<TransactionResponse> AddTransactionAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Symbol is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Type))
            throw new ArgumentException("Type is required.", nameof(request));
        if (request.Quantity == 0)
            throw new ArgumentException("Quantity cannot be zero.", nameof(request));
        if (request.PriceAtTransaction < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(request));

        var validTypes = new[] { "Buy", "Swap", "Sell", "Fee", "Deposit", "Withdraw" };
        if (!validTypes.Contains(request.Type, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Type must be one of: {string.Join(", ", validTypes)}.", nameof(request));

        var userId = _currentUser.RequireUserId();
        var price = request.PriceAtTransaction;
        var tradingTypes = new[] { "Buy", "Sell", "Swap" };
        if (price == 0 && tradingTypes.Contains(request.Type, StringComparer.OrdinalIgnoreCase))
        {
            var fetched = await _priceService.GetPriceInUsdAsync(request.Symbol.Trim(), request.Date, cancellationToken);
            if (fetched.HasValue && fetched.Value > 0)
                price = fetched.Value;
        }

        var transaction = new Transaction
        {
            UserId = userId,
            Id = Guid.NewGuid(),
            Symbol = request.Symbol.Trim(),
            Type = request.Type.Trim(),
            Quantity = request.Quantity,
            PriceAtTransaction = price,
            Fee = request.Fee,
            Date = request.Date.Kind == DateTimeKind.Utc ? request.Date : DateTime.SpecifyKind(request.Date, DateTimeKind.Utc),
            BaseCurrency = request.BaseCurrency?.Trim() ?? string.Empty,
            Notes = request.Notes?.Trim().Length > 0 ? request.Notes.Trim() : null
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToResponse(transaction);
    }

    public async Task<PagedListDto<TransactionResponse>> GetTransactionsPageAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var q = _context.Transactions.Where(t => t.UserId == userId);
        var total = await q.CountAsync(cancellationToken);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        var items = await q
            .OrderByDescending(t => t.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedListDto<TransactionResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages
        };
    }

    public async Task<int> BackfillPricesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var tradingTypes = new[] { "Buy", "Sell", "Swap" };
        var toFill = await _context.Transactions
            .Where(t => t.UserId == userId && t.PriceAtTransaction == 0 && tradingTypes.Contains(t.Type))
            .ToListAsync(cancellationToken);
        var updated = 0;
        foreach (var t in toFill)
        {
            var price = await _priceService.GetPriceInUsdAsync(t.Symbol, t.Date, cancellationToken);
            if (price.HasValue && price.Value > 0)
            {
                t.PriceAtTransaction = price.Value;
                updated++;
            }
            await Task.Delay(200, cancellationToken);
        }
        if (updated > 0)
            await _context.SaveChangesAsync(cancellationToken);
        return updated;
    }

    public async Task<int> AddTransactionsAsync(IReadOnlyList<CreateTransactionRequest> requests, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.RequireUserId();
        var validTypes = new[] { "Buy", "Swap", "Sell", "Fee", "Deposit", "Withdraw" };
        var list = new List<Transaction>();
        foreach (var r in requests)
        {
            if (string.IsNullOrWhiteSpace(r.Symbol) || string.IsNullOrWhiteSpace(r.Type) || r.Quantity == 0 || r.PriceAtTransaction < 0)
                continue;
            if (!validTypes.Contains(r.Type, StringComparer.OrdinalIgnoreCase))
                continue;
            list.Add(new Transaction
            {
                UserId = userId,
                Id = Guid.NewGuid(),
                Symbol = r.Symbol.Trim(),
                Type = r.Type.Trim(),
                Quantity = r.Quantity,
                PriceAtTransaction = r.PriceAtTransaction,
                Fee = r.Fee,
                Date = r.Date.Kind == DateTimeKind.Utc ? r.Date : DateTime.SpecifyKind(r.Date, DateTimeKind.Utc),
                BaseCurrency = r.BaseCurrency?.Trim() ?? string.Empty,
                Notes = r.Notes?.Trim().Length > 0 ? r.Notes.Trim() : null
            });
        }
        if (list.Count == 0) return 0;
        _context.Transactions.AddRange(list);
        await _context.SaveChangesAsync(cancellationToken);
        return list.Count;
    }

    private static TransactionResponse MapToResponse(Transaction t)
    {
        var netValue = t.Quantity * t.PriceAtTransaction + t.Fee;
        return new TransactionResponse
        {
            Id = t.Id,
            Symbol = t.Symbol,
            Type = t.Type,
            Quantity = t.Quantity,
            PriceAtTransaction = t.PriceAtTransaction,
            Fee = t.Fee,
            NetValue = netValue,
            Date = t.Date,
            BaseCurrency = t.BaseCurrency,
            Notes = t.Notes
        };
    }
}
