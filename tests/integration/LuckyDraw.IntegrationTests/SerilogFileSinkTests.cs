using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LuckyDraw.IntegrationTests;

/// <summary>
/// `51:OBS-05` / `52:OBS-05` 守护：应用日志必须**落盘为文件**，且正文可离线检索。
///
/// 为什么需要这条：`52:OBS-05` 实测应用日志只有 `WriteTo: Console`（未落盘为文件）→ 日志正文**无法离线检索**，
/// `TC-81`（日志不含明文密码 / token）只能改以 `AuditLog` / `DrawRequest.RequestHash` / `User.PasswordHash`
/// 三面旁证代替 —— 旁证不能消除「判据面受限」本身。本组用例把两件事钉死：
/// ① **接线**：宿主**实际生效**的 `Serilog:WriteTo` 中必须有 File sink，且其 `outputTemplate` 与 Console
/// **逐字一致**（同一套检索方式，避免同一段日志出现两种格式、两套检索口径）；
/// ② **效果**：经宿主 `ILoggerFactory` 写一条唯一标记后，磁盘上必须出现**含该标记正文**的日志文件。
/// 只断言配置而不看落盘效果 = 同义反复：路径不可写、承载 sink 的程序集缺失、滚动名算法不符都测不出来。
///
/// **不依赖数据库 / Redis**：本组不挂 <see cref="IntegrationFixture"/>（不触碰业务表与缓存），
/// 只启动宿主读配置 + 写一条日志 —— 该断言面与业务数据无关，基础设施缺失时仍应可执行
/// （`docs/development-spec.md` 7.6 用例独立性：自带隔离前提，不依赖顺序与共享可变夹具）。
/// </summary>
public class SerilogFileSinkTests : IClassFixture<LuckyDrawApiFactory>
{
    /// <summary>被守护的 File sink 相对路径（与 `appsettings.json` 取值逐字一致；`-` 后缀由按天滚动补日期）。</summary>
    private const string ExpectedPath = "logs/luckydraw-.log";

    /// <summary>滚动粒度：按天。</summary>
    private const string ExpectedRollingInterval = "Day";

    /// <summary>保留文件数上限：防日志无限增长（Serilog 默认 31）。</summary>
    private const string ExpectedRetainedFileCountLimit = "7";

    /// <summary>标记检索的最长等待窗口（File sink 逐条 flush，此窗口仅覆盖文件系统可见性）。</summary>
    private static readonly TimeSpan MarkerTimeout = TimeSpan.FromSeconds(5);

    private readonly LuckyDrawApiFactory _factory;

    /// <summary>注入宿主工厂（类级夹具：同一进程内只多起一个宿主）。</summary>
    /// <param name="factory">集成宿主工厂。</param>
    public SerilogFileSinkTests(LuckyDrawApiFactory factory) => _factory = factory;

    /// <summary>宿主实际生效的 `Serilog:WriteTo` 配置节（读回值，非源码常量）。</summary>
    private IConfigurationSection WriteToSection =>
        _factory.Services.GetRequiredService<IConfiguration>().GetSection("Serilog:WriteTo");

    /// <summary>落盘断言：宿主生效配置里必须有 File sink，且与 Console 共用逐字一致的 <c>outputTemplate</c>。</summary>
    [Fact]
    public void FileSink_IsConfiguredAndMatchesConsoleTemplate()
    {
        var sinks = WriteToSection.GetChildren().ToList();
        sinks.Should().NotBeEmpty("宿主实际生效的 `Serilog:WriteTo` 必须至少有一项");

        var console = FindSink(sinks, "Console");
        var file = FindSink(sinks, "File");

        console.Should().NotBeNull("Console sink 是既有基线，不得在本轮改动中被移除");
        file.Should().NotBeNull(
            "`51:OBS-05` / `52:OBS-05`：应用日志必须落盘为文件（Serilog File sink），" +
            "否则日志正文无法离线检索，只能退回以表数据交叉验证");

        file!["Args:path"].Should().Be(ExpectedPath, "落盘路径为相对应用 CWD 的 `logs/luckydraw-.log`");
        file["Args:rollingInterval"].Should().Be(ExpectedRollingInterval, "按天滚动：`-` 后缀由滚动自动补日期");
        file["Args:retainedFileCountLimit"].Should().Be(ExpectedRetainedFileCountLimit, "保留 7 份，防日志无限增长");
        file["Args:outputTemplate"].Should().Be(
            console!["Args:outputTemplate"],
            "File 与 Console 必须共用同一套 outputTemplate（逐字一致），否则同一段日志有两种格式、检索方式分叉");
    }

    /// <summary>
    /// 落盘效果断言：写一条唯一标记后，按天滚动的日志文件必须出现且**正文含该标记**。
    /// 标记含非 ASCII 字符 —— `Serilog.Sinks.File` 默认按 UTF-8 写文件（与控制台宿主的 GBK 不同），
    /// 编码不符时本断言即变红（本文件按 UTF-8 解码检索）。
    /// </summary>
    [Fact]
    public async Task FileSink_WritesLogFileContainingBody()
    {
        // 走宿主 DI 的 ILoggerFactory：应用自身代码正是这条路径（`UseSerilog` 已把 ILoggerFactory 接到 Serilog），
        // 故本断言覆盖「配置 → sink → 磁盘」全链
        var marker = $"OBS05LOGMARK-{Guid.NewGuid():N} 日志落盘检索标记";
        _factory.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(SerilogFileSinkTests))
            .LogWarning("{Marker}", marker);

        var directory = Path.Combine(Directory.GetCurrentDirectory(), Path.GetDirectoryName(ExpectedPath)!);
        var filePath = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(ExpectedPath)
            + DateTime.Now.ToString("yyyyMMdd")
            + Path.GetExtension(ExpectedPath));

        var content = await ReadWhenContainsAsync(filePath, marker, MarkerTimeout);

        content.Should().NotBeNull(
            $"应用日志必须按 {ExpectedRollingInterval} 滚动落到 {filePath}（相对应用 CWD）且正文含本次写入的标记；"
            + $"当前目录内容：{Describe(directory)}");
    }

    /// <summary>按名称查找 `WriteTo` 项（大小写不敏感，与配置文件写法解耦）。</summary>
    /// <param name="sinks">`WriteTo` 子项集合。</param>
    /// <param name="name">sink 名称。</param>
    private static IConfigurationSection? FindSink(IEnumerable<IConfigurationSection> sinks, string name) =>
        sinks.SingleOrDefault(section => string.Equals(section["Name"], name, StringComparison.OrdinalIgnoreCase));

    /// <summary>在超时窗口内轮询读取日志文件，返回含标记的正文；窗口内始终未出现则返回 <c>null</c>。</summary>
    /// <param name="filePath">日志文件绝对路径。</param>
    /// <param name="marker">检索标记。</param>
    /// <param name="timeout">最长等待时长。</param>
    private static async Task<string?> ReadWhenContainsAsync(string filePath, string marker, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (File.Exists(filePath))
            {
                // FileShare.ReadWrite：File sink 运行期持有该文件（读侧不得独占打开）
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
                var content = await reader.ReadToEndAsync();
                if (content.Contains(marker, StringComparison.Ordinal))
                {
                    return content;
                }
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        return null;
    }

    /// <summary>目录内容快照（失败诊断用；目录不存在时如实说明）。</summary>
    /// <param name="directory">日志目录。</param>
    private static string Describe(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return $"目录不存在：{directory}";
        }

        var files = Directory.GetFiles(directory, "*.log");
        return files.Length == 0
            ? $"无 .log 文件：{directory}"
            : string.Join("；", files.Select(file => $"{Path.GetFileName(file)}（{new FileInfo(file).Length} B）"));
    }
}
