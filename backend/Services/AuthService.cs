using System.Text.RegularExpressions;
using CryptoTracker.Data;
using CryptoTracker.DTOs;
using CryptoTracker.Interfaces;
using CryptoTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Services;

public class AuthService : IAuthService
{
    private static readonly Regex ValidUsername = new(
        @"^[a-z0-9][a-z0-9._-]{2,31}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;

    public AuthService(AppDbContext db, JwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = NormalizeUsername(request.Username);
        var password = request.Password ?? "";
        ValidatePassword(password);
        if (!IsValidUsername(username))
            throw new ArgumentException("Username must be 3–32 characters: letters, digits, . _ - (cannot start with . _ -).");

        if (await _db.Users.AnyAsync(u => u.Username == username, cancellationToken))
            throw new ArgumentException("Username is already taken.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedUtc = DateTime.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var (token, exp) = _jwt.CreateAccessToken(user.Id, user.Username);
        return new AuthResponseDto { AccessToken = token, ExpiresAtUtc = exp, Username = user.Username };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = NormalizeUsername(request.Username);
        var password = request.Password ?? "";
        if (string.IsNullOrEmpty(username) || password.Length == 0)
            throw new ArgumentException("Username and password are required.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new ArgumentException("Invalid username or password.");

        var (token, exp) = _jwt.CreateAccessToken(user.Id, user.Username);
        return new AuthResponseDto { AccessToken = token, ExpiresAtUtc = exp, Username = user.Username };
    }

    private static string NormalizeUsername(string? value) =>
        (value ?? "").Trim().ToLowerInvariant();

    private static bool IsValidUsername(string normalized) =>
        normalized.Length >= 3 && ValidUsername.IsMatch(normalized);

    private static void ValidatePassword(string password)
    {
        if (password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
    }
}
