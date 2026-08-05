namespace ErpSystem.Core.Services;

/// <summary>
/// Normalises caller-supplied paging arguments.
///
/// <para>Paged endpoints take <c>pageNumber</c> / <c>pageSize</c> straight from the query string. Two
/// things go wrong without clamping: <c>pageNumber=0</c> produces a negative <c>Skip()</c> and throws,
/// and <c>pageSize</c> is unbounded, so <c>?pageSize=1000000</c> pulls an entire table into memory.</para>
/// </summary>
public static class PagingGuard
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize     = 200;

    public static (int PageNumber, int PageSize) Clamp(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
            pageNumber = 1;

        if (pageSize < 1)
            pageSize = DefaultPageSize;
        else if (pageSize > MaxPageSize)
            pageSize = MaxPageSize;

        return (pageNumber, pageSize);
    }
}
