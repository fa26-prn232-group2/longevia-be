namespace Longevia.Shared.Pagination;

public readonly record struct PaginationQuery(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public PaginationQuery Normalized() =>
        new(Math.Max(Page, 1), Math.Clamp(PageSize, 1, MaxPageSize));
}
