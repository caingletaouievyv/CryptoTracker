namespace CryptoTracker.Exceptions;

/// <summary>Maps to a specific HTTP status in <see cref="Middleware.ExceptionHandlingMiddleware"/>.</summary>
public sealed class AppHttpException : Exception
{
    public int StatusCode { get; }

    public AppHttpException(int statusCode, string message) : base(message) =>
        StatusCode = statusCode;
}
