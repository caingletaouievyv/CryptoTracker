using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoTracker.DTOs;
using CryptoTracker.Exceptions;

namespace CryptoTracker.Middleware;

public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppHttpException ex)
        {
            _logger.LogWarning(ex, "HTTP {Code}: {Message}", ex.StatusCode, ex.Message);
            await WriteJsonAsync(context, ex.StatusCode, ApiResponse<object?>.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Bad request: {Message}", ex.Message);
            await WriteJsonAsync(context, StatusCodes.Status400BadRequest, ApiResponse<object?>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteJsonAsync(context, StatusCodes.Status500InternalServerError,
                ApiResponse<object?>.Fail("Something went wrong"));
        }
    }

    private static async Task WriteJsonAsync<T>(HttpContext context, int statusCode, ApiResponse<T> body)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        await context.Response.WriteAsJsonAsync(body, JsonOptions);
    }
}
