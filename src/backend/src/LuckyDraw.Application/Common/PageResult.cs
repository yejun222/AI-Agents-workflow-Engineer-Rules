namespace LuckyDraw.Application.Common;

/// <summary>通用分页响应（规范 6.3）。</summary>
/// <typeparam name="T">列表项类型。</typeparam>
public class PageResult<T>
{
    /// <summary>当前页数据。</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>总记录数。</summary>
    public int TotalCount { get; set; }

    /// <summary>当前页码（从 1 起）。</summary>
    public int PageIndex { get; set; }

    /// <summary>每页条数。</summary>
    public int PageSize { get; set; }
}
