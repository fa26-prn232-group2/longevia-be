namespace Diet.Application.Features.Foods;

/// <summary>Phân trang theo HTTP contract của Diet.</summary>
public sealed record FoodPageResponse(IReadOnlyList<FoodResponse> Items, int PageNumber,
    int PageSize, long TotalCount, int TotalPages, bool HasPreviousPage, bool HasNextPage);
