namespace HealthyMind.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public PagedResult<TOut> Map<TOut>(Func<T, TOut> map) =>
        new(Items.Select(map).ToList(), Page, PageSize, TotalCount);
}

public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 100;

    public int SafePage => Math.Max(Page, 1);

    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}
