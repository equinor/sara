using Microsoft.EntityFrameworkCore;

namespace api.Utilities
{
    public class PagedList<T> : List<T>
    {
        public int CurrentPage { get; private set; }
        public int TotalPages { get; private set; }
        public int PageSize { get; private set; }
        public int TotalCount { get; private set; }

        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        public PagedList(List<T> items, int currentPage, int pageSize, int totalCount)
        {
            CurrentPage = currentPage;
            PageSize = pageSize;
            TotalCount = totalCount;
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            AddRange(items);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Design",
            "CA1000:Do not declare static members on generic types",
            Justification = "It's ok"
        )]
        public static async Task<PagedList<T>> ToPagedListAsync(
            IQueryable<T> source,
            int pageNumber,
            int pageSize
        )
        {
            int totalCount = await source.CountAsync();
            var items = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();
            return new PagedList<T>(items, pageNumber, pageSize, totalCount);
        }
    }

    public class QueryParameters
    {
        public const string PaginationHeader = "X-Pagination";
        public const int MaxPageSize = 1000;

        /// <summary>
        /// Defaults to '1' if left empty
        /// </summary>
        public int PageNumber { get; set; } = 1;

        private int _pageSize = 20;

        /// <summary>
        /// <para>Defaults to '10' if left empty.</para>
        /// Max value is '1000'
        /// </summary>
        public int PageSize
        {
            get { return _pageSize; }
            set { _pageSize = (value > MaxPageSize) ? MaxPageSize : value; }
        }

        /// <summary>
        /// Can be ordered by several parameters.
        /// <para>Use 'desc' after a parameter name to order it Descending (default is Ascending)</para>
        /// <para>Format: "OrderBy=Id, Name desc, DateCreated"</para>
        /// </summary>
        public string OrderBy { get; set; } = "";
    }
}
