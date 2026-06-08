using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TransactionController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionController(ITransactionService transactionService) =>
        _transactionService = transactionService;

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransactionResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TransactionResponse>>> AddTransaction(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _transactionService.AddTransactionAsync(request, cancellationToken);
        return CreatedAtAction(
            nameof(GetAllTransactions),
            new { page = 1, pageSize = 10 },
            ApiResponse<TransactionResponse>.Ok(result, "Created"));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedListDto<TransactionResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedListDto<TransactionResponse>>>> GetAllTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var paged = await _transactionService.GetTransactionsPageAsync(page, pageSize, cancellationToken);
        return Ok(ApiResponse<PagedListDto<TransactionResponse>>.Ok(paged));
    }

    [HttpPost("backfill-prices")]
    [ProducesResponseType(typeof(ApiResponse<BackfillPricesResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<BackfillPricesResultDto>>> BackfillPrices(CancellationToken cancellationToken)
    {
        var updated = await _transactionService.BackfillPricesAsync(cancellationToken);
        return Ok(ApiResponse<BackfillPricesResultDto>.Ok(new BackfillPricesResultDto { Updated = updated }));
    }
}
