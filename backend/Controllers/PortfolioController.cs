using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CryptoTracker.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PortfolioController : ControllerBase
{
    private readonly IHoldingService _holdingService;

    public PortfolioController(IHoldingService holdingService) =>
        _holdingService = holdingService;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PortfolioDashboardResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PortfolioDashboardResponse>>> GetPortfolio(CancellationToken cancellationToken)
    {
        var data = await _holdingService.GetPortfolioAsync(cancellationToken);
        return Ok(ApiResponse<PortfolioDashboardResponse>.Ok(data));
    }
}
