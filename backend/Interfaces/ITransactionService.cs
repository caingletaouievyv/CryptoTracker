using CryptoTracker.DTOs;

namespace CryptoTracker.Interfaces;

public interface ITransactionService
{
    Task<TransactionResponse> AddTransactionAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default);

    Task<PagedListDto<TransactionResponse>> GetTransactionsPageAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<int> BackfillPricesAsync(CancellationToken cancellationToken = default);

    Task<int> AddTransactionsAsync(IReadOnlyList<CreateTransactionRequest> requests, CancellationToken cancellationToken = default);
}
