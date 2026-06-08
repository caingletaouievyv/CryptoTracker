using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Authorize]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly IOkxSyncService _okxSyncService;
    private readonly IBinanceSyncService _binanceSyncService;

    public SyncController(IOkxSyncService okxSyncService, IBinanceSyncService binanceSyncService)
    {
        _okxSyncService = okxSyncService;
        _binanceSyncService = binanceSyncService;
    }

    /// <summary>Sync SPOT Buy/Sell from OKX bills. Credentials: config (Okx:*) or optional body. Optional body: after, limit.</summary>
    [HttpPost("okx/transactions")]
    [ProducesResponseType(typeof(ApiResponse<OkxSyncResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ApiResponse<OkxSyncResultDto>>> SyncOkxTransactions(
        [FromBody] OkxSyncTransactionsRequest? body,
        CancellationToken cancellationToken = default)
    {
        var result = await _okxSyncService.SyncOkxSpotTransactionsAsync(body, cancellationToken);
        return Ok(ApiResponse<OkxSyncResultDto>.Ok(result));
    }

    /// <summary>Sync Spot account trades from Binance (myTrades per symbol). Body: apiKey, secretKey, symbols[]. Or use Binance:* in config.</summary>
    [HttpPost("binance/transactions")]
    [ProducesResponseType(typeof(ApiResponse<OkxSyncResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ApiResponse<OkxSyncResultDto>>> SyncBinanceTransactions(
        [FromBody] BinanceSyncTransactionsRequest? body,
        CancellationToken cancellationToken = default)
    {
        var result = await _binanceSyncService.SyncBinanceSpotTradesAsync(body, cancellationToken);
        return Ok(ApiResponse<OkxSyncResultDto>.Ok(result));
    }
}
