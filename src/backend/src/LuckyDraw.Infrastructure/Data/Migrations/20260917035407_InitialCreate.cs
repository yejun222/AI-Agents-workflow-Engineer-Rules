using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LuckyDraw.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AuditLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    OperatorId = table.Column<int>(type: "int", nullable: true),
                    OperateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Module = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperationType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetObject = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BeforeJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AfterJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLog", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DrawRequest",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PrizeItemId = table.Column<int>(type: "int", nullable: true),
                    IsWin = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RemainingAttempts = table.Column<int>(type: "int", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrawRequest", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PrizeItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ShortName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    Stock = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrizeItem", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PasswordHash = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserDrawQuota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DrawDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UsedCount = table.Column<int>(type: "int", nullable: false),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDrawQuota", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WinningRecord",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    PrizeItemId = table.Column<int>(type: "int", nullable: false),
                    PrizeName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    CreateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WinningRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WinningRecord_PrizeItem_PrizeItemId",
                        column: x => x.PrizeItemId,
                        principalTable: "PrizeItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WinningRecord_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "PrizeItem",
                columns: new[] { "Id", "Code", "CreateTime", "DisplayOrder", "IsEnabled", "Name", "ShortName", "Stock", "Type", "UpdateTime", "Weight" },
                values: new object[,]
                {
                    { 1, "prize-keyboard", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1, true, "一等奖 · 机械键盘", "一等奖", 3, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 1 },
                    { 2, "prize-earbuds", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 2, true, "二等奖 · 蓝牙耳机", "二等奖", 10, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3 },
                    { 3, "prize-mug", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, true, "三等奖 · 定制马克杯", "三等奖", 50, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10 },
                    { 4, "prize-coupon", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, true, "幸运奖 · 平台优惠券", "幸运奖", 200, 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 20 },
                    { 5, "no-prize", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 5, true, "谢谢参与", "谢谢参与", 0, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 66 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_Module_Time",
                table: "AuditLog",
                columns: new[] { "Module", "OperateTime" });

            migrationBuilder.CreateIndex(
                name: "UX_DrawRequest_User_Key",
                table: "DrawRequest",
                columns: new[] { "UserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrizeItem_Enabled_Order",
                table: "PrizeItem",
                columns: new[] { "IsEnabled", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_PrizeItem_Code",
                table: "PrizeItem",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_User_UserName",
                table: "User",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UserDrawQuota_User_Date",
                table: "UserDrawQuota",
                columns: new[] { "UserId", "DrawDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WinningRecord_PrizeItemId",
                table: "WinningRecord",
                column: "PrizeItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WinningRecord_User_Time",
                table: "WinningRecord",
                columns: new[] { "UserId", "CreateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLog");

            migrationBuilder.DropTable(
                name: "DrawRequest");

            migrationBuilder.DropTable(
                name: "UserDrawQuota");

            migrationBuilder.DropTable(
                name: "WinningRecord");

            migrationBuilder.DropTable(
                name: "PrizeItem");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
