namespace LuckyDraw.Application.Common;

/// <summary>
/// 通用分页请求基类（规范 6.2）：<c>pageIndex</c> 从 1 起，<c>pageSize</c> 默认 10、上限 100。
/// 非法值按规范取整而非报错（如 pageSize &gt; 100 → 按 100，pageIndex &lt; 1 → 按 1）。
/// </summary>
public class PageQuery
{
    /// <summary>每页条数上限。</summary>
    public const int MaxPageSize = 100;

    /// <summary>每页条数默认值。</summary>
    public const int DefaultPageSize = 10;

    /// <summary>页码（从 1 起）。</summary>
    public int PageIndex { get; set; } = 1;

    /// <summary>每页条数（默认 10、上限 100）。</summary>
    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>排序字段名（本接口白名单为空，传入即忽略）。</summary>
    public string? SortField { get; set; }

    /// <summary>排序方向（本接口固定倒序，传入即忽略）。</summary>
    public string? SortOrder { get; set; }

    /// <summary>规范化后的页码（≥ 1）。</summary>
    public int NormalizedPageIndex => PageIndex < 1 ? 1 : PageIndex;

    /// <summary>规范化后的每页条数（1–100，非法值回落默认 10）。</summary>
    public int NormalizedPageSize => PageSize <= 0
        ? DefaultPageSize
        : Math.Min(PageSize, MaxPageSize);
}
