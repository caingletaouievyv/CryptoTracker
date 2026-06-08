using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CryptoTracker.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config) => _config = config;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(Guid userId, string username)
    {
        var issuer = _config["Jwt:Issuer"] ?? "CryptoTracker";
        var audience = _config["Jwt:Audience"] ?? "CryptoTracker";
        var key = _config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required (min 32 characters).");
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        var minutes = Math.Clamp(_config.GetValue("Jwt:AccessTokenMinutes", 10080), 5, 525600); // default 7d, max ~1y
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
