using LuckyDraw.Domain.Entities;
using LuckyDraw.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LuckyDraw.Infrastructure.Data;

/// <summary>
/// 应用数据上下文（6 表，架构 §4.2）。索引 / 字段长度 / 种子全部在 <see cref="OnModelCreating"/> 中显式声明
/// （规范 4.4：字符串字段必须 HasMaxLength，高频筛选排序字段必须建索引）。
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>种子数据固定时间戳（HasData 要求确定性常量，避免每次生成迁移产生差异）。</summary>
    private static readonly DateTime SeedTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>构造函数注入选项。</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>用户表。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>奖池条目表。</summary>
    public DbSet<PrizeItem> PrizeItems => Set<PrizeItem>();

    /// <summary>中奖记录表。</summary>
    public DbSet<WinningRecord> WinningRecords => Set<WinningRecord>();

    /// <summary>每日次数聚合表。</summary>
    public DbSet<UserDrawQuota> UserDrawQuotas => Set<UserDrawQuota>();

    /// <summary>抽奖请求流水表（幂等权）。</summary>
    public DbSet<DrawRequest> DrawRequests => Set<DrawRequest>();

    /// <summary>审计日志表。</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureUser(modelBuilder);
        ConfigurePrizeItem(modelBuilder);
        ConfigureWinningRecord(modelBuilder);
        ConfigureUserDrawQuota(modelBuilder);
        ConfigureDrawRequest(modelBuilder);
        ConfigureAuditLog(modelBuilder);
    }

    /// <summary>User（MOD-01）：用户名大小写不敏感唯一（列级 collation 保证，FR-01-1）。</summary>
    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("User");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName)
                .IsRequired()
                .HasMaxLength(20)
                .UseCollation("utf8mb4_0900_ai_ci");
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(100);
            entity.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.HasIndex(x => x.UserName).IsUnique().HasDatabaseName("UX_User_UserName");

            // 逻辑删除过滤；唯一性校验侧必须 IgnoreQueryFilters()（规范 4.4 / 5.3 选项②）
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    /// <summary>PrizeItem（MOD-02 / MOD-03）：种子 5 条默认奖池（D-09 / FR-11）。</summary>
    private static void ConfigurePrizeItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PrizeItem>(entity =>
        {
            entity.ToTable("PrizeItem");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(32);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(50);
            entity.Property(x => x.ShortName).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Type).HasConversion<int>().IsRequired();
            entity.Property(x => x.Weight).IsRequired();
            entity.Property(x => x.Stock).IsRequired();
            entity.Property(x => x.DisplayOrder).IsRequired();
            entity.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);
            entity.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_PrizeItem_Code");
            entity.HasIndex(x => new { x.IsEnabled, x.DisplayOrder }).HasDatabaseName("IX_PrizeItem_Enabled_Order");
            entity.HasQueryFilter(x => !x.IsDeleted);

            entity.HasData(
                new PrizeItem
                {
                    Id = 1,
                    Code = "prize-keyboard",
                    Name = "一等奖 · 机械键盘",
                    ShortName = "一等奖",
                    Type = PrizeItemType.Physical,
                    Weight = 1,
                    Stock = 3,
                    DisplayOrder = 1,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreateTime = SeedTime,
                    UpdateTime = SeedTime
                },
                new PrizeItem
                {
                    Id = 2,
                    Code = "prize-earbuds",
                    Name = "二等奖 · 蓝牙耳机",
                    ShortName = "二等奖",
                    Type = PrizeItemType.Physical,
                    Weight = 3,
                    Stock = 10,
                    DisplayOrder = 2,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreateTime = SeedTime,
                    UpdateTime = SeedTime
                },
                new PrizeItem
                {
                    Id = 3,
                    Code = "prize-mug",
                    Name = "三等奖 · 定制马克杯",
                    ShortName = "三等奖",
                    Type = PrizeItemType.Physical,
                    Weight = 10,
                    Stock = 50,
                    DisplayOrder = 3,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreateTime = SeedTime,
                    UpdateTime = SeedTime
                },
                new PrizeItem
                {
                    Id = 4,
                    Code = "prize-coupon",
                    Name = "幸运奖 · 平台优惠券",
                    ShortName = "幸运奖",
                    Type = PrizeItemType.Virtual,
                    Weight = 20,
                    Stock = 200,
                    DisplayOrder = 4,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreateTime = SeedTime,
                    UpdateTime = SeedTime
                },
                new PrizeItem
                {
                    Id = 5,
                    Code = "no-prize",
                    Name = "谢谢参与",
                    ShortName = "谢谢参与",
                    Type = PrizeItemType.NoPrize,
                    Weight = 66,
                    Stock = 0,
                    DisplayOrder = 5,
                    IsEnabled = true,
                    IsDeleted = false,
                    CreateTime = SeedTime,
                    UpdateTime = SeedTime
                });
        });
    }

    /// <summary>WinningRecord（MOD-04）：只读记录，索引 (UserId, CreateTime) 覆盖倒序分页。</summary>
    private static void ConfigureWinningRecord(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WinningRecord>(entity =>
        {
            entity.ToTable("WinningRecord");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PrizeName).IsRequired().HasMaxLength(50);
            entity.Property(x => x.IsDeleted).IsRequired().HasDefaultValue(false);
            entity.HasIndex(x => new { x.UserId, x.CreateTime }).HasDatabaseName("IX_WinningRecord_User_Time");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PrizeItem>().WithMany().HasForeignKey(x => x.PrizeItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(x => !x.IsDeleted);
        });
    }

    /// <summary>UserDrawQuota（MOD-03 / D-02）：唯一 (UserId, DrawDate)，跨日重置 = 新行。</summary>
    private static void ConfigureUserDrawQuota(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserDrawQuota>(entity =>
        {
            entity.ToTable("UserDrawQuota");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DrawDate).HasColumnType("date").IsRequired();
            entity.Property(x => x.UsedCount).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.DrawDate })
                .IsUnique()
                .HasDatabaseName("UX_UserDrawQuota_User_Date");
        });
    }

    /// <summary>DrawRequest（MOD-05）：唯一 (UserId, IdempotencyKey) 为幂等正确性底线。</summary>
    private static void ConfigureDrawRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DrawRequest>(entity =>
        {
            entity.ToTable("DrawRequest");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(64);
            entity.Property(x => x.RequestHash).HasMaxLength(64);
            entity.Property(x => x.IsWin).IsRequired();
            entity.Property(x => x.RemainingAttempts).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.IdempotencyKey })
                .IsUnique()
                .HasDatabaseName("UX_DrawRequest_User_Key");
        });
    }

    /// <summary>AuditLog（MOD-06）：只增不改，无 FK、无 UpdateTime / IsDeleted。</summary>
    private static void ConfigureAuditLog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.UserAgent).HasMaxLength(200);
            entity.Property(x => x.Module).IsRequired().HasMaxLength(50);
            entity.Property(x => x.OperationType).IsRequired().HasMaxLength(50);
            entity.Property(x => x.TargetObject).HasMaxLength(64);
            entity.Property(x => x.BeforeJson).HasColumnType("text");
            entity.Property(x => x.AfterJson).HasColumnType("text");
            entity.HasIndex(x => new { x.Module, x.OperateTime }).HasDatabaseName("IX_AuditLog_Module_Time");
        });
    }
}
