using Microsoft.EntityFrameworkCore;

namespace NguyenHoaiPhong_1681.DTOs;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>Tham số phân trang chung: page mặc định 1, pageSize mặc định 20, tối đa 100.</summary>
public class PageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize switch { < 1 => 20, > 100 => 100, _ => PageSize };
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedAsync<T>(this IQueryable<T> query, PageQuery paging, CancellationToken ct = default)
    {
        var page = paging.SafePage;
        var size = paging.SafePageSize;
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<T>(items, page, size, total);
    }
}
