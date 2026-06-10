using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jenkins.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHandoffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContainerReleaseHandoff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuildId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BuildArtifactId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeployableComponentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeployableUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeploymentReleaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SemanticVersion = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ArtifactUri = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedByPrincipal = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SettledAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerReleaseHandoff", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerReleaseHandoff_BuildArtifactId",
                table: "ContainerReleaseHandoff",
                column: "BuildArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerReleaseHandoff_BuildId_Status",
                table: "ContainerReleaseHandoff",
                columns: new[] { "BuildId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerReleaseHandoff_DeploymentReleaseId",
                table: "ContainerReleaseHandoff",
                column: "DeploymentReleaseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerReleaseHandoff");
        }
    }
}
