using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Metering.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsageRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Meter = table.Column<int>(type: "int", nullable: false),
                    MeterType = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<double>(type: "float", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Feature = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Repository = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Service = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Environment = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CostUsd = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    RateVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecord_EventId_Direction",
                table: "UsageRecord",
                columns: new[] { "EventId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecord_Meter_Model",
                table: "UsageRecord",
                columns: new[] { "Meter", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecord_OccurredAtUtc",
                table: "UsageRecord",
                column: "OccurredAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsageRecord");
        }
    }
}
