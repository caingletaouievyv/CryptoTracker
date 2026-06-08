namespace CryptoTracker.DTOs;

/// <summary>Paginated list payload inside ApiResponse.Data (see docs/architecture.md).</summary>
public class PagedListDto<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
