using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jenkins.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineRun",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggeredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Steps = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRun_PipelineId",
                table: "PipelineRun",
                column: "PipelineId");

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRun_StartedAtUtc",
                table: "PipelineRun",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PipelineRun");
        }
    }
}
