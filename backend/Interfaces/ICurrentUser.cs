namespace CryptoTracker.Interfaces;

/// <summary>Authenticated user from JWT (set by authentication middleware).</summary>
public interface ICurrentUser
{
    /// <summary>Throws if the request is not authenticated.</summary>
    Guid RequireUserId();
}
