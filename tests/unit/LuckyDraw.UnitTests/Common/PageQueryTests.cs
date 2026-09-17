using FluentAssertions;
using LuckyDraw.Application.Common;

namespace LuckyDraw.UnitTests.Common;

/// <summary>分页基类（规范 6.2）：pageIndex 从 1 起、pageSize 默认 10 上限 100，非法值取整而非报错。</summary>
public class PageQueryTests
{
    /// <summary>默认值：第 1 页、每页 10 条。</summary>
    [Fact]
    public void Defaults_AreFirstPageWithTenItems()
    {
        var query = new PageQuery();

        query.NormalizedPageIndex.Should().Be(1);
        query.NormalizedPageSize.Should().Be(10);
    }

    /// <summary>pageIndex &lt; 1 取整为 1（含负数与 0）。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageIndex_BelowOne_FallsBackToOne(int pageIndex)
    {
        new PageQuery { PageIndex = pageIndex }.NormalizedPageIndex.Should().Be(1);
    }

    /// <summary>pageSize 超过上限按上限处理（防止一次性全量查询）。</summary>
    [Fact]
    public void PageSize_AboveMaximum_IsClamped()
    {
        new PageQuery { PageSize = 1000 }.NormalizedPageSize.Should().Be(PageQuery.MaxPageSize);
    }

    /// <summary>pageSize 非法（≤ 0）回落默认 10。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void PageSize_NotPositive_FallsBackToDefault(int pageSize)
    {
        new PageQuery { PageSize = pageSize }.NormalizedPageSize.Should().Be(PageQuery.DefaultPageSize);
    }

    /// <summary>合法区间内的值原样保留（含边界值 1 与 100）。</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(7, 25)]
    public void PageIndexAndSize_WithinRange_ArePreserved(int pageIndex, int pageSize)
    {
        var query = new PageQuery { PageIndex = pageIndex, PageSize = pageSize };

        query.NormalizedPageIndex.Should().Be(pageIndex);
        query.NormalizedPageSize.Should().Be(pageSize);
    }
}
