using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Deployment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAspireApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspireApplication",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AppHostPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    KubeContext = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Namespace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspireApplication", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspireApplicationRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AppHostPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    KubeContext = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Namespace = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspireApplicationRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspireApplication_Name",
                table: "AspireApplication",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspireApplicationRun_ApplicationId",
                table: "AspireApplicationRun",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AspireApplicationRun_Status",
                table: "AspireApplicationRun",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspireApplication");

            migrationBuilder.DropTable(
                name: "AspireApplicationRun");
        }
    }
}
