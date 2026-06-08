using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class HoldingsController : ControllerBase
{
    private readonly IHoldingService _holdingService;

    public HoldingsController(IHoldingService holdingService) =>
        _holdingService = holdingService;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<HoldingResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HoldingResponse>>>> GetHoldings(CancellationToken cancellationToken)
    {
        var list = await _holdingService.GetHoldingsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HoldingResponse>>.Ok(list));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object?>>> SetHoldings([FromBody] SetHoldingsRequest? request, CancellationToken cancellationToken)
    {
        if (request?.Holdings == null)
            throw new ArgumentException("Holdings array is required.");
        await _holdingService.SetHoldingsAsync(request.Holdings, cancellationToken);
        return Ok(ApiResponse<object?>.Ok(null, ""));
    }
}
