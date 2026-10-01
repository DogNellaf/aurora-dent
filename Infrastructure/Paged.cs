using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure
{
    public interface IPaged
    {
        int Page { get; }
        int TotalPages { get; }
        int Total { get; }
    }

    /// <summary>One page of a larger result set.</summary>
    public class Paged<T> : IPaged
    {
        public const int DefaultPageSize = 20;

        public List<T> Items { get; init; } = new();
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = DefaultPageSize;
        public int Total { get; init; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;

        public Paged<TOut> Map<TOut>(Func<List<T>, List<TOut>> map) =>
            new() { Items = map(Items), Page = Page, PageSize = PageSize, Total = Total };
    }

    public static class PagedExtensions
    {
        public static async Task<Paged<T>> ToPagedAsync<T>(this IQueryable<T> query, int page, int pageSize = Paged<T>.DefaultPageSize)
        {
            var total = await query.CountAsync();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, pages);

            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return new Paged<T> { Items = items, Page = page, PageSize = pageSize, Total = total };
        }
    }
}
