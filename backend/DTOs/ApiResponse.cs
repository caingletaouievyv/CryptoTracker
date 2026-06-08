using System.Text.Json.Serialization;

namespace CryptoTracker.DTOs;

/// <summary>Standard API envelope (see docs/architecture.md).</summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }

    public string Message { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "") =>
        new() { Success = true, Message = message ?? "", Data = data };

    public static ApiResponse<T> Fail(string message) =>
        new() { Success = false, Message = message, Data = default };
}
