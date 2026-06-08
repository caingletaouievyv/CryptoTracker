using System.Security.Claims;
using CryptoTracker.Exceptions;
using CryptoTracker.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CryptoTracker.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor) =>
        _httpContextAccessor = httpContextAccessor;

    public Guid RequireUserId()
    {
        var sub = _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var id))
            throw new AppHttpException(StatusCodes.Status401Unauthorized, "Unauthorized.");
        return id;
    }
}
